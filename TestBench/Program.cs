using System;
using System.IO;
using zsyncnet;
namespace TestBench
{
    class Program
    {
        static void Main(string[] args)
        {
            var downloaded =
                Zsync.Sync(
                    new Uri(
                        "http://u.beowulfso.com/synctest/@CUP_Terrains_Core/addons/cup_terrains_ca_structures.pbo.zsync"),
                    new DirectoryInfo("C:\\arma3\\zsyncnet"));
        }
    }
}
