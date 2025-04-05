using ASE.Utils;
using ASE.SII2Parser;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ASE {
    public partial class App : Application {
        private void Application_Startup(object sender, StartupEventArgs e) {
            // If the argument contains a path to a sii file, decode it and quit.
            if (e.Args.Length == 0) return;
            string path = string.Join(" ", e.Args);
            if (!path.EndsWith(".sii", StringComparison.OrdinalIgnoreCase)) return;
            // if file does not exist, quit.
            if (!File.Exists(path)) return;

            // Verbose for debugging
            MessageBox.Show("Decoding " + path, "Decoding", MessageBoxButton.OK, MessageBoxImage.Information);

            var bytes = File.ReadAllBytes(path);
            if (!SIIParser2.IsSupported(bytes)) {
                MessageBox.Show("This sii file is not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            if (bytes[0..4].SequenceEqual(SIIParser2.HEADER_STRING)) {
                MessageBox.Show("This sii file is already decoded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            var sii = SIIParser2.Parse(bytes);
            FileStream fs = new(path, FileMode.Create);
            StreamWriter sw = new(fs, BetterThanStupidMS.UTF8);
            sii.WriteTo(sw);
            sw.Close();
            fs.Close();

            MessageBox.Show("Successfully decoded the file.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            Current.Shutdown();
        }
    }
}
