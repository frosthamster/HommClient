using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class Program
    {
        public static void Main(string[] args)
        {
            //            homm.ulearn.me
            //            127.0.0.1
            if (args.Length == 0)
                args = new[] { "127.0.0.1", "18700" };
            var controller = new GameController(args[0], int.Parse(args[1]));
            while (true)
            {
                try
                {
                    controller.DoBestStep();
                }
                catch (CVARC.V2.ClientException e)
                {
                    Console.WriteLine("----------------------------------------");
                    Console.WriteLine("Game finished");
                    Console.WriteLine("----------------------------------------");
                    break;
                }
            }
        }
    }
}