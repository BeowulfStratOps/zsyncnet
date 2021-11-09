using System;
using System.IO;
using zsyncnet;
namespace TestBench
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var filename in new[]
            {
                "cup_terrains_buildings", "cup_terrains_ca_air2_dummy", "cup_terrains_ca_plants_e2",
                "cup_terrains_ca_structures"
            })
            {
                var start = DateTime.Now;
                var uri = $"http://u.beowulfso.com/synctest/@CUP_Terrains_Core/addons/{filename}.pbo.zsync";
                Zsync.Sync(new Uri(uri), new DirectoryInfo("C:\\arma3\\zsyncnet"));
                Console.WriteLine($"{filename} took {(DateTime.Now - start).TotalSeconds:F2} seconds");
            }

            // cup_terrains_ca_plants_e2
            //  us: 3.47s, 127528 md4 calls. found 111435 blocks. 31 ranges
            //  zsync: <1s with download., 118652 md4 calls
            //  seq: 3.9s, 111426 calls, 111426 blocks, 22 ranges
            // cup_terrains_ca_structures
            //  us: 13s, 554443 md4 calls. found 262738 blocks. 425 ranges
            //  zsync: 30s with download. probably like 5 for comparing, 327299 md4 calls
            //  seq: 11.75s, 262680 calls, 262682 found, 351 ranges
            // cup_terrains_buildings
            //  us: 33s, 1968744 md4 calls. found 61715. 504 ranges
            //  zsync: 5s, 222609 md4 calls
            // seq: 14s, 64137calls, found 61437, 235 ranges.
        }
    }
}
