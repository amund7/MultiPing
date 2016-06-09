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
using System.Windows.Shapes;
using OxyPlot;
using OxyPlot.WindowsForms;
using OxyPlot.Axes;
using OxyPlot.Wpf;

namespace MultiPing {

  public class MainViewModel {
 
    public string Title { get; private set; }

    public List<DataPoint> Points { get; private set; }
    public LineSeries[] line=new LineSeries[256];
    public List<DataPoint>[] point = new List<DataPoint>[256];
    public double lasttime;


    public MainViewModel() {
      this.Title = "Hosts";
      this.Points = new List<DataPoint>();
      for (int i = 0; i < 256; i++) {
        point[i] = new List<DataPoint>();
        line[i] = new LineSeries() { StrokeThickness = 1, LineStyle=LineStyle.Solid };
      }
    }


    public void Add(Double y) {
      if (Points.Count() > 100)
        Points.RemoveAt(0);
      Points.Add(new DataPoint(lasttime = OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), y));      
    }

    public void Add(int ip,Double y) {
      var then = DateTime.Now.Subtract(new TimeSpan(0,1,0));
      if (point[ip].Count>0)
        if (point[ip].ElementAt(0).X < OxyPlot.Axes.TimeSpanAxis.ToDouble(then))
          point[ip].RemoveAt(0);
      point[ip].Add(new DataPoint(lasttime = OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), y));
    }

    public void Clean(int ip) {
      var then = DateTime.Now.Subtract(new TimeSpan(0, 0, 10));
      if (point[ip].Count > 0)
        if (point[ip].ElementAt(0).X < OxyPlot.Axes.TimeSpanAxis.ToDouble(then))
          point[ip].RemoveAt(0);
    }


    public void Clear() {
      Points.Clear();
    }
  }


  /// <summary>
  /// Interaction logic for Graph.xaml
  /// </summary>
  public partial class Graph : Window {
    public MainViewModel model;
    public Graph() {
      InitializeComponent();
      model = new MainViewModel();
      this.DataContext = model;
      Plot1.Axes.Add(new OxyPlot.Wpf.DateTimeAxis());
      Plot1.Axes.Add(new OxyPlot.Wpf.LinearAxis());
      //CompositionTarget.Rendering += CompositionTargetRendering;
      //Plot1.
      for (int i = 0; i < 255; i++) {
        Plot1.Series.Add(model.line[i]);
        model.line[i].ItemsSource = model.point[i];
      }
    }

 /*   private void CompositionTargetRendering(object sender, EventArgs e) {
      //model.UpdateModel();
      Plot.RefreshPlot(true);
    }*/

  }
}
