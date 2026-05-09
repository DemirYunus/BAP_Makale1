using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BAP.DAL;
using Gurobi;
using BAP.GANTT;

namespace BAP.MIP
{
    public class GurobiOptimizer
    {
		public void SolveSchedulingModel()
		{
			try
			{
				// 1. VERİTABANINDAN VERİLERİN ÇEKİLMESİ (Entity Framework)
				int numSiparis = 0;
				int numBilesen = 0;
				int numMakine = 0;

				double[,,] p; // İşlem süreleri: p[sipariş, makine, bileşen]
				double[,] D;  // Talep: D[sipariş, bileşen]
				double[] dd;  // Termin: dd[sipariş]

				using (var db = new u0987408_AcdmyContext())
				{
					// Genel Parametreler
					var genParam = db.GeneralParam.FirstOrDefault();
					if (genParam != null)
					{
						numSiparis = genParam.SiparisSayisi ?? 0;
						numBilesen = genParam.AltBilesenSayisi ?? 0;
						numMakine = genParam.MakineSayisi ?? 0;
					}

					// Dizileri 1-based indeksleme kullanmak için boyutları +1 ayarlıyoruz
					p = new double[numSiparis + 1, numMakine + 1, numBilesen + 1];
					D = new double[numSiparis + 1, numBilesen + 1];
					dd = new double[numSiparis + 1];

					// İşlem Süreleri (ProcessingTime)
					foreach (var pt in db.ProcessingTime.ToList())
					{
						if (pt.Siparis.HasValue && pt.Makine.HasValue && pt.AltBilesen.HasValue && pt.Sure.HasValue)
						{
							p[pt.Siparis.Value, pt.Makine.Value, pt.AltBilesen.Value] = pt.Sure.Value;
						}
					}

					// Talepler (Demand)
					foreach (var dem in db.Demand.ToList())
					{
						if (dem.Siparis.HasValue && dem.AltBilesen.HasValue && dem.Adet.HasValue)
						{
							D[dem.Siparis.Value, dem.AltBilesen.Value] = dem.Adet.Value;
						}
					}

					// Termin Süreleri (DueDate)
					foreach (var due in db.DueDate.ToList())
					{
						if (due.Siparis.HasValue && due.TeslimTarihi.HasValue)
						{
							dd[due.Siparis.Value] = due.TeslimTarihi.Value;
						}
					}
				}

				// Statik Parametreler (GAMS'teki Scalar değerler)
				double s = 30.0;            // Sabit Hazırlık Süresi
				double delta_max = 60.0;    // Senkronizasyon Toleransı
				double W1 = 0.0;            // Cmax Ağırlığı
				double W2 = 1.0;            // Toplam Gecikme Ağırlığı
				double BigM = 100000.0;     // Büyük M Değeri

				// 2. GUROBI ÇEVRESİ VE MODELİNİN OLUŞTURULMASI
				GRBEnv env = new GRBEnv();
				GRBModel model = new GRBModel(env);
				model.ModelName = "TekstilOrme_Cizelgeleme";

				// 3. KARAR DEĞİŞKENLERİNİN TANIMLANMASI
				GRBVar[,,] S_var = new GRBVar[numSiparis + 1, numMakine + 1, numBilesen + 1];
				GRBVar[,,] C_ikc = new GRBVar[numSiparis + 1, numMakine + 1, numBilesen + 1];
				GRBVar[,,] Q = new GRBVar[numSiparis + 1, numMakine + 1, numBilesen + 1];
				GRBVar[,,] Z = new GRBVar[numSiparis + 1, numMakine + 1, numBilesen + 1];
				GRBVar[] C_i = new GRBVar[numSiparis + 1];
				GRBVar[] T_i = new GRBVar[numSiparis + 1];
				GRBVar Cmax = model.AddVar(0.0, GRB.INFINITY, 0.0, GRB.CONTINUOUS, "Cmax");

				Dictionary<string, GRBVar> Y = new Dictionary<string, GRBVar>();

				// Sipariş Bazlı Değişkenler
				for (int i = 1; i <= numSiparis; i++)
				{
					C_i[i] = model.AddVar(0.0, GRB.INFINITY, 0.0, GRB.CONTINUOUS, $"C_i_{i}");
					T_i[i] = model.AddVar(0.0, GRB.INFINITY, 0.0, GRB.CONTINUOUS, $"T_i_{i}");

					// Sipariş, Makine ve Bileşen Bazlı Değişkenler
					for (int m = 1; m <= numMakine; m++)
					{
						for (int c = 1; c <= numBilesen; c++)
						{
							if (p[i, m, c] > 0) // Sadece işlem süresi > 0 olanlar için değişken yarat (Pre-processing)
							{
								S_var[i, m, c] = model.AddVar(0.0, GRB.INFINITY, 0.0, GRB.CONTINUOUS, $"S_var_{i}_{m}_{c}");
								C_ikc[i, m, c] = model.AddVar(0.0, GRB.INFINITY, 0.0, GRB.CONTINUOUS, $"C_ikc_{i}_{m}_{c}");

								// GAMS'teki Infeasible hatasını çözen 10000 üst sınırı Q için burada ayarlanıyor!
								Q[i, m, c] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, $"Q_{i}_{m}_{c}");
								Z[i, m, c] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, $"Z_{i}_{m}_{c}");
							}
						}
					}
				}

				// Y (Öncelik/Sıralama) Değişkeni (Sadece çakışma ihtimali olan aynı makinedeki görevler için)
				for (int m = 1; m <= numMakine; m++)
				{
					for (int i = 1; i <= numSiparis; i++)
					{
						for (int c = 1; c <= numBilesen; c++)
						{
							if (p[i, m, c] == 0) continue;

							for (int ip = 1; ip <= numSiparis; ip++)
							{
								for (int cp = 1; cp <= numBilesen; cp++)
								{
									if (p[ip, m, cp] == 0) continue;

									// Simetriyi önlemek için GAMS'teki ord mantığı
									if ((i * 100 + c) < (ip * 100 + cp))
									{
										string key = $"{i}_{c}_{ip}_{cp}_{m}";
										Y[key] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, $"Y_{key}");
									}
								}
							}
						}
					}
				}

				// 4. AMAÇ FONKSİYONU
				GRBLinExpr objExpr = 0.0;
				objExpr.AddTerm(W1, Cmax);
				for (int i = 1; i <= numSiparis; i++)
				{
					objExpr.AddTerm(W2, T_i[i]);
				}
				model.SetObjective(objExpr, GRB.MINIMIZE);

				// 5. KISITLAR (CONSTRAINTS)
				for (int i = 1; i <= numSiparis; i++)
				{
					// Cmax Kısıtı
					model.AddConstr(Cmax >= C_i[i], $"Eq_Cmax_{i}");

					// Gecikme Kısıtı (Tardiness)
					model.AddConstr(T_i[i] >= C_i[i] - dd[i], $"Eq_Tardiness_{i}");

					for (int c = 1; c <= numBilesen; c++)
					{
						// 1. Talep Karşılama Kısıtı (Eq_Demand)
						if (D[i, c] > 0)
						{
							GRBLinExpr demandExpr = 0.0;
							for (int m = 1; m <= numMakine; m++)
							{
								if (p[i, m, c] > 0)
								{
									demandExpr.AddTerm(1.0, Q[i, m, c]);
								}
							}
							model.AddConstr(demandExpr == D[i, c], $"Eq_Demand_{i}_{c}");
						}

						for (int m = 1; m <= numMakine; m++)
						{
							if (p[i, m, c] > 0)
							{
								// 2. Makine Atama Kısıtı (Eq_Eligibility)
								model.AddConstr(Q[i, m, c] <= BigM * Z[i, m, c], $"Eq_Eligibility_{i}_{m}_{c}");

								// 3. İşlem ve Tamamlanma Süreleri Kısıtı (Eq_CompTime)
								model.AddConstr(C_ikc[i, m, c] >= S_var[i, m, c] + s * Z[i, m, c] + p[i, m, c] * Q[i, m, c] - BigM * (1 - Z[i, m, c]), $"Eq_CompTime_{i}_{m}_{c}");

								// 5. Atama Yapılmayan İşlemlerin Sıfırlanması
								model.AddConstr(S_var[i, m, c] <= BigM * Z[i, m, c], $"Eq_NotAssignedS_{i}_{m}_{c}");
								model.AddConstr(C_ikc[i, m, c] <= BigM * Z[i, m, c], $"Eq_NotAssignedC_{i}_{m}_{c}");

								// 7. Sipariş Tamamlanma Süresi (Eq_OrderComp)
								model.AddConstr(C_i[i] >= C_ikc[i, m, c], $"Eq_OrderComp_{i}_{m}_{c}");

								// 6. Montaj Senkronizasyonu (MAD)
								for (int mp = 1; mp <= numMakine; mp++)
								{
									for (int cp = 1; cp <= numBilesen; cp++)
									{
										if (p[i, mp, cp] > 0 && c < cp)
										{
											model.AddConstr(C_ikc[i, m, c] - C_ikc[i, mp, cp] <= delta_max + BigM * (2 - Z[i, m, c] - Z[i, mp, cp]), $"Eq_Sync1_{i}_{c}_{cp}_{m}_{mp}");
											model.AddConstr(C_ikc[i, mp, cp] - C_ikc[i, m, c] <= delta_max + BigM * (2 - Z[i, m, c] - Z[i, mp, cp]), $"Eq_Sync2_{i}_{c}_{cp}_{m}_{mp}");
										}
									}
								}
							}
						}
					}
				}

				// 4. Çakışmayı Önleme Kısıtları (Eq_Prec1 ve Eq_Prec2)
				for (int m = 1; m <= numMakine; m++)
				{
					for (int i = 1; i <= numSiparis; i++)
					{
						for (int c = 1; c <= numBilesen; c++)
						{
							if (p[i, m, c] == 0) continue;

							for (int ip = 1; ip <= numSiparis; ip++)
							{
								for (int cp = 1; cp <= numBilesen; cp++)
								{
									if (p[ip, m, cp] == 0) continue;

									if ((i * 100 + c) < (ip * 100 + cp))
									{
										string key = $"{i}_{c}_{ip}_{cp}_{m}";

										// S_var(i,m,c) >= C_ikc(ip,m,cp) - BigM*Y - BigM*(2-Z-Z)
										model.AddConstr(S_var[i, m, c] >= C_ikc[ip, m, cp] - BigM * Y[key] - BigM * (2 - Z[i, m, c] - Z[ip, m, cp]), $"Eq_Prec1_{key}");

										// S_var(ip,m,cp) >= C_ikc(i,m,c) - BigM*(1-Y) - BigM*(2-Z-Z)
										model.AddConstr(S_var[ip, m, cp] >= C_ikc[i, m, c] - BigM * (1 - Y[key]) - BigM * (2 - Z[i, m, c] - Z[ip, m, cp]), $"Eq_Prec2_{key}");
									}
								}
							}
						}
					}
				}

				// GAMS Option: OptCR = 0.05
				model.Parameters.MIPGap = 0.05;

				// Modeli Çöz
				Console.WriteLine("Optimizasyon başlatılıyor...");
				model.Optimize();

				// 6. SONUÇLARIN EKRANA YAZDIRILMASI
				if (model.Status == GRB.Status.OPTIMAL)
				{
					List<GanttTask> ganttData = new List<GanttTask>();
					int taskCounter = 1;

					for (int i = 1; i <= numSiparis; i++)
					{
						for (int m = 1; m <= numMakine; m++)
						{
							for (int c = 1; c <= numBilesen; c++)
							{
								// Eğer bu atama yapıldıysa (Q > 0)
								if (p[i, m, c] > 0 && Q[i, m, c].X > 0.5)
								{
									ganttData.Add(new GanttTask
									{
										TaskId = taskCounter++,
										Machine = $"M{m}",
										Order = $"O{i}",
										Component = $"c{c}",
										Start = S_var[i, m, c].X,
										End = C_ikc[i, m, c].X,
										Qty = (int)Q[i, m, c].X,
										SetupTime = s,
										// Sipariş bazlı renk ataması
										Color = i == 1 ? "bg-blue-500" : (i == 2 ? "bg-green-500" : "bg-red-500")
									});
								}
							}
						}
					}

					// BAP.GANTT projesini çağır ve çizelgeyi göster
					GanttVisualizer visualizer = new GanttVisualizer();
					// Teslim tarihlerini (dd) görselleştiriciye göndermek için hazırlıyoruz
					Dictionary<string, double> dueDates = new Dictionary<string, double>();
					for (int i = 1; i <= numSiparis; i++)
					{
						dueDates.Add($"O{i}", dd[i]);
					}

					// Yeni parametre ile metodu çağırıyoruz
					visualizer.ShowGantt(ganttData, Cmax.X, model.ObjVal, dueDates);
				}
				else
				{
					Console.WriteLine($"\nModel çözülemedi. Gurobi Status: {model.Status}");
				}

				// Kaynakları serbest bırak
				model.Dispose();
				env.Dispose();
			}
			catch (GRBException e)
			{
				Console.WriteLine($"Gurobi Hata Kodu: {e.ErrorCode}. Hata: {e.Message}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Sistem Hatası: {ex.Message}");
			}
		}
	}
}
