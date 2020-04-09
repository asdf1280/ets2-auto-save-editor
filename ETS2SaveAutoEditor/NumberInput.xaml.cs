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

namespace ETS2SaveAutoEditor
{
    public class NumberInputBox
    {
        public static int Show(string title, string description)
        {
            var inst = new NumberInput(title, description);
            inst.ShowDialog();
            return inst.number;
        }
    }

    /// <summary>
    /// NumberInput.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NumberInput : Window
    {
        public NumberInput(string title, string description)
        {
            InitializeComponent();

            Opacity = 0;

            var anim = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.2)));
            anim.DecelerationRatio = 1;
            BeginAnimation(Window.OpacityProperty, anim);

            var blur = new BlurEffect();
            blur.Radius = 10;
            blur.RenderingBias = RenderingBias.Quality;
            Effect = blur;
            var anim0 = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(0.6)));
            anim0.DecelerationRatio = 1;
            blur.BeginAnimation(BlurEffect.RadiusProperty, anim0);

            TitleLabel.Content = title;
            Description.Text = description;
        }

        private void Title_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public int number;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            number = -1;
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                int result = int.Parse(Input.Text);
                number = result;
                Close();
            }
            catch (Exception)
            {
                MessageBox.Show("올바른 숫자를 입력하세요.", "오류");
                Input.Text = "";
            }
        }
    }
}
