using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ASE
{
    public class StringInputBox
    {
        public static string Show(string title, string description)
        {
            StringInput inst = new StringInput(title, description);
            _ = inst.ShowDialog();
            return inst.text;
        }
    }

    /// <summary>
    /// NumberInput.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class StringInput : Window
    {
        public StringInput(string title, string description)
        {
            InitializeComponent();

            Opacity = 0;

            var anim = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.1)))
            {
                DecelerationRatio = 1
            };
            BeginAnimation(Window.OpacityProperty, anim);

            var blur = new BlurEffect
            {
                Radius = 10,
                RenderingBias = RenderingBias.Quality
            };
            Effect = blur;
            var anim0 = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.4)))
            {
                DecelerationRatio = 1
            };
            blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);

            Title = title;
            Description.Text = description;
        }

        private void Title_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public string text = null;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            text = null;
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            text = Input.Text;
            Close();
        }
    }
}
