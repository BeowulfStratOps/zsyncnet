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
            // cup_terrains_ca_plants_e2
            //  us: 6.4s, 127137 md4 calls
            //  zsync: <1s with download., 118652 md4 calls
            // cup_terrains_ca_structures
            //  us: 21s, 550799 md4 calls
            //  zsync: 30s with download. probably like 5 for comparing, 327299 md4 calls
        }
    }
}
