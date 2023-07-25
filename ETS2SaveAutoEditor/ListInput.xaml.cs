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

    public class ListInputBox
    {
        public static int Show(string title, string description, string[] items)
        {
            var inst = new ListInput(title, description, items);
            inst.ShowDialog();
            return inst.number;
        }
    }

    /// <summary>
    /// ListInput.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ListInput : Window
    {
        public ListInput(string title, string description, string[] items)
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
            foreach(var item in items)
                ItemList.Items.Add(item);
        }

        private void Title_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public int number = -1;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            number = -1;
            Hide();
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            int result = ItemList.SelectedIndex;
            if(result == -1)
            {
                MessageBox.Show("항목을 선택하세요.", "오류");
                return;
            }
            number = result;
            Hide();
            Close();
        }

        private void Description_MouseDown(object sender, MouseButtonEventArgs e) {
            Clipboard.SetText(Description.Text);
        }
    }
}