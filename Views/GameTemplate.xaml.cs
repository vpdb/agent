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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	public partial class GameTemplate : UserControl
	{

		public static readonly DependencyProperty GameProperty = DependencyProperty.Register("Game", typeof(Game), typeof(GameTemplate), new PropertyMetadata(default(Game), GamePropertyChanged));

		static void GamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var source = d as GameTemplate;
			source.bind();
		}

		public Game Game
		{
			get { return GetValue(GameProperty) as Game; }
			set
			{
				SetValue(GameProperty, value);
			}
		}

		public GameTemplate()
		{
			InitializeComponent();
		}

		private void bind()
		{

		}
	}
}
