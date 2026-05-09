using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace BAP.GANTT
{
	public class GanttTask
	{
		public int TaskId { get; set; }
		public string Machine { get; set; }
		public string Order { get; set; }
		public string Component { get; set; }
		public double Start { get; set; }
		public double End { get; set; }
		public int Qty { get; set; }
		public double SetupTime { get; set; }
		public string Color { get; set; }
	}

	public class GanttVisualizer
	{
		public void ShowGantt(List<GanttTask> tasks, double cMax, double totalDelay, Dictionary<string, double> dueDates)
		{
			string jsonTasks = JsonConvert.SerializeObject(tasks);
			string jsonDueDates = JsonConvert.SerializeObject(dueDates);

			string cMaxStr = cMax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
			string totalDelayStr = totalDelay.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

			string htmlTemplate = @"
<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='UTF-8'>
    <script src='https://cdn.tailwindcss.com'></script>
    <title>Gelişmiş Optimum Gantt</title>
    <style>
        .setup-pattern {
            background-image: repeating-linear-gradient(45deg, transparent, transparent 4px, rgba(0,0,0,0.3) 4px, rgba(0,0,0,0.3) 8px);
            background-color: #4b5563;
        }
        /* Çizgilerin görev çubuklarının üstünde görünmesi için z-index yönetimi */
        .gantt-line-overlay { z-index: 50; }
        .gantt-task-bar { z-index: 10; }
    </style>
</head>
<body class='bg-gray-50 p-6 font-sans text-gray-800'>
    
    <div class='mb-6'>
        <h1 class='text-2xl font-bold'>Gurobi Optimum Gantt Çizelgesi</h1>
        <p class='text-sm text-gray-600 mt-1'>
            Toplam Gecikme (Obj): <span class='font-bold text-red-600'>###TOTAL_DELAY### dk</span> | 
            Cmax: <span class='font-bold text-blue-600'>###CMAX### dk</span>
        </p>
    </div>
    
    <div class='flex flex-wrap gap-4 mb-6 bg-white p-3 rounded border border-gray-200 shadow-sm'>
        <div class='flex items-center gap-2'><div class='w-4 h-4 bg-blue-500 rounded-sm'></div><span class='text-sm font-medium'>O1</span></div>
        <div class='flex items-center gap-2'><div class='w-4 h-4 bg-green-500 rounded-sm'></div><span class='text-sm font-medium'>O2</span></div>
        <div class='flex items-center gap-2'><div class='w-4 h-4 bg-red-500 rounded-sm'></div><span class='text-sm font-medium'>O3</span></div>
        <div class='flex items-center gap-2 ml-4'><div class='w-0 h-4 border-l-2 border-dashed border-gray-800'></div><span class='text-sm font-medium text-xs'>Termin Çizgisi</span></div>
        <div class='flex items-center gap-2 ml-4'><div class='w-4 h-4 setup-pattern rounded-sm'></div><span class='text-sm font-medium text-xs'>Setup</span></div>
    </div>

    <div id='gantt-container' class='bg-white rounded-xl shadow border border-gray-300 overflow-x-auto relative'>
    </div>

    <script>
        const tasks = ###TASKS_JSON###;
        const dueDates = ###DUEDATES_JSON###;
        const cMax = ###CMAX###;
        
        const machines = [...new Set(tasks.map(t => t.Machine))].sort();
        const maxDueDate = Math.max(...Object.values(dueDates));
        const maxTime = Math.ceil(Math.max(cMax, maxDueDate) / 100) * 100 + 100; 
        const container = document.getElementById('gantt-container');

        // Gecikme hesaplama: Her siparişin en geç biten işini bul
        const orderCompletions = tasks.reduce((acc, t) => {
            acc[t.Order] = Math.max(acc[t.Order] || 0, t.End);
            return acc;
        }, {});

        const orderDelays = {};
        Object.keys(dueDates).forEach(order => {
            orderDelays[order] = Math.max(0, orderCompletions[order] - dueDates[order]);
        });

        let html = `<div class='relative' style='min-width: 1000px; width: 100%;'>`;

        // 1. Zaman Ekseni (Header)
        html += `<div class='flex border-b border-gray-300 bg-gray-200 font-bold text-sm h-10'>
                    <div class='w-24 shrink-0 border-r border-gray-300 flex items-center justify-center bg-gray-200 z-[60]'>Makineler</div>
                    <div class='flex-1 relative'>`;
        
        for (let i = 0; i <= maxTime; i += 100) {
            const leftPct = (i / maxTime) * 100;
            html += `<div class='absolute border-l border-gray-400 h-full' style='left: ${leftPct}%'>
                        <span class='absolute bottom-1 -translate-x-1/2 text-xs font-semibold text-gray-700'>${i}</span>
                     </div>`;
        }
        html += `</div></div>`;

        // 2. Makine Satırları
        machines.forEach(m => {
            html += `<div class='flex border-b border-gray-200 h-16 hover:bg-gray-50 transition-colors'>
                        <div class='w-24 shrink-0 flex items-center justify-center font-bold border-r border-gray-300 bg-white z-20'>${m}</div>
                        <div class='flex-1 relative py-2'>`;

            tasks.filter(t => t.Machine === m).forEach(t => {
                const leftPct = (t.Start / maxTime) * 100;
                const totalDuration = t.End - t.Start;
                const widthPct = (totalDuration / maxTime) * 100;
                const setupWidth = (t.SetupTime / totalDuration) * 100;
                
                html += `<div class='absolute top-2 bottom-2 rounded shadow border border-black/20 flex overflow-hidden gantt-task-bar' 
                              style='left: ${leftPct}%; width: ${widthPct}%;'>
                            <div class='h-full setup-pattern' style='width: ${setupWidth}%;'></div>
                            <div class='h-full flex-1 flex flex-col items-center justify-center text-white ${t.Color}'>
                                <span class='text-[10px] font-bold truncate'>${t.Order}-${t.Component}</span>
                                <span class='text-[8px] opacity-90'>${t.Qty} Adet</span>
                            </div>
                         </div>`;
            });
            html += `</div></div>`;
        });

        // 3. EN ÜSTTEKİ TERMİN ÇİZGİLERİ VE ETİKETLERİ
        // Bu div makinelerin ve barların üstüne binecek (z-50)
        html += `<div class='absolute top-0 bottom-0 pointer-events-none gantt-line-overlay' style='left: 6rem; right: 0;'>`;
        
        const sortedDds = Object.entries(dueDates)
            .filter(([_, dd]) => dd > 0)
            .sort((a, b) => a[1] - b[1]);

        let lastX = -100;
        let stackLevel = 0;

        sortedDds.forEach(([order, dd]) => {
            const leftPct = (dd / maxTime) * 100;
            
            // Etiket çakışma kontrolü: Eğer çizgiler birbirine çok yakınsa etiketi aşağı kaydır
            if (leftPct - lastX < 8) { stackLevel++; } 
            else { stackLevel = 0; }
            lastX = leftPct;

            let colorClass = 'border-gray-600 text-gray-700';
            if (order === 'O1') colorClass = 'border-blue-600 text-blue-700';
            else if (order === 'O2') colorClass = 'border-green-600 text-green-700';
            else if (order === 'O3') colorClass = 'border-red-600 text-red-700';

            const delay = orderDelays[order] || 0;
            const topOffset = 8 + (stackLevel * 32); // Çakışma durumunda 32px aşağı öteler

            html += `
                <div class='absolute h-full border-l-2 border-dashed ${colorClass.split(' ')[0]} opacity-70' style='left: ${leftPct}%'>
                    <div class='absolute -translate-x-1/2 bg-white/95 px-2 py-1 rounded shadow-lg border border-gray-200 whitespace-nowrap ${colorClass.split(' ')[1]}' 
                         style='top: ${topOffset}px;'>
                        <div class='font-bold text-[10px]'>${order} Termin: ${dd}</div>
                        <div class='text-[9px] font-semibold ${delay > 0 ? 'text-red-600' : 'text-green-600'}'>
                            Gecikme: ${delay.toFixed(1)} dk
                        </div>
                    </div>
                </div>`;
        });

        html += `</div></div>`;
        container.innerHTML = html;
    </script>
</body>
</html>";

			htmlTemplate = htmlTemplate.Replace("###TASKS_JSON###", jsonTasks);
			htmlTemplate = htmlTemplate.Replace("###DUEDATES_JSON###", jsonDueDates);
			htmlTemplate = htmlTemplate.Replace("###CMAX###", cMaxStr);
			htmlTemplate = htmlTemplate.Replace("###TOTAL_DELAY###", totalDelayStr);

			string filePath = Path.Combine(Path.GetTempPath(), "Gurobi_Gantt_Result.html");
			File.WriteAllText(filePath, htmlTemplate, Encoding.UTF8);

			try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); }
			catch { System.Diagnostics.Process.Start(filePath); }
		}
	}
}
