using System.Collections.Generic;

namespace FindusWebApp.Helpers
{
    public class WooCredentials
    {
        public static readonly string Name = "WooCredentials";

        //public List<WooKeys> WooKeys { get; set; }
        public Dictionary<string, WooKeys> WooKeys { get; set; }
    }
}
