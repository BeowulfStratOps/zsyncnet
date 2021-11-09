using System;
using System.IO;
using zsyncnet;
namespace TestBench
{
    class Program
    {
        static void Main(string[] args)
        {
            var start = DateTime.Now;
            const string uri = "http://u.beowulfso.com/synctest/@CUP_Terrains_Core/addons/cup_terrains_ca_structures.pbo.zsync";
            Zsync.Sync(new Uri(uri), new DirectoryInfo("C:\\arma3\\zsyncnet"));
            Console.WriteLine($"Took {(DateTime.Now-start).TotalSeconds:F2} seconds");


            // cup_terrains_ca_plants_e2
            //  us: 3.47s, 127528 md4 calls
            //  zsync: <1s with download., 118652 md4 calls
            // cup_terrains_ca_structures
            //  us: 13s, 554443 md4 calls
            //  zsync: 30s with download. probably like 5 for comparing, 327299 md4 calls
            // cup_terrains_buildings
            //  us: 33s, 1968744 md4 calls
        }
    }
}
