using BAP.DAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BAP.MIP;

namespace BAP.INTERFACE
{
	internal class Program
	{
		static void Main(string[] args)
		{
			GurobiOptimizer gurobiOptimizer = new GurobiOptimizer();
			gurobiOptimizer.SolveSchedulingModel();

			Console.ReadLine();
		}
	}
}
