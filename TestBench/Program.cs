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
                Console.WriteLine(
                    FormattableString.Invariant($"{filename} took {(DateTime.Now - start).TotalSeconds:F2} seconds."));
            }
        }
    }
}
