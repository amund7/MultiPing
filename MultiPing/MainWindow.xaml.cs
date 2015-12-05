﻿/* MultiPing by Amund Børsand (amund.borsand@nov.com)
 * march 2015
 * Idea by Jan Øyvind Lyche */

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

using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Reflection;
using OxyPlot;


namespace MultiPing {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  /// 


  public partial class MainWindow : Window {

    Ping[] pingSender = new Ping[256];
    static bool continuous = false;
    static bool gethost = true;
    public static string manuf;
    public static MainWindow mainWin;
    public static Dispatcher disp;

    // Our main data structure
    private ObservableCollection<PingResult> pingResults;

    // Public property, for the UI to access
    public ObservableCollection<PingResult> PingResults {
      get {
        return this.pingResults;
      }
    }


    Graph graph;
    Graph speed;

    public MainWindow() {
      InitializeComponent();
      pingResults = new ObservableCollection<PingResult>();
      Results.ItemsSource = pingResults;
      GetIPButton_Click(null, null);
      for (int i=0; i<255; i++)
        pingSender[i] = new Ping();
      manuf = File.ReadAllText("manuf.txt");
      mainWin = this;
      disp = this.Dispatcher;

      // Get version. Crashes if within visual studio, so we have to catch that.
      try {
        var version = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion;
        this.Title = "MultiPing v." + version.ToString();
      } catch (Exception) {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        this.Title = "MultPing development build " + version.ToString();
      }

      graph = new Graph();
      graph.Show();
      speed = new Graph();
      speed.Show();

      //PingResults.Count.PropertyChanged+=

    }


    public void doPing(Ping pingSender, string who) {
      int timeout = 2000;
      try {
        pingSender.PingCompleted += new PingCompletedEventHandler(PingCompletedCallback);
        pingSender.SendAsync(who, timeout, who);
      } catch (PingException) { /*pingSender.SendAsyncCancel(); Thread.Sleep(2000); pingSender.SendAsync(who, timeout, who);*/ }
    }


    private async void PingCompletedCallback(object sender, PingCompletedEventArgs e) {
      if (e.Cancelled) {
        ((Ping)sender).PingCompleted -= PingCompletedCallback;
        return;
      }
      PingReply reply = e.Reply;
      DisplayReply((string)e.UserState,reply);
      ((Ping)sender).PingCompleted -= PingCompletedCallback;

      //speed.Plot1.InvalidatePlot(true);

      if (continuous) {
        await Task.Delay(1000);
        doPing((Ping)sender, ((string)e.UserState));
      }
    }


    public void DisplayReply(string addr, PingReply reply) {
      if (reply == null) {
        return;
      }
      if (reply.Status == IPStatus.Success) {
        speed.model.Add(reply.Address.GetAddressBytes()[3],reply.RoundtripTime);
        speed.Plot1.InvalidatePlot(true);
        var hits = PingResults.Where(x => x.ip.Equals(reply.Address));
        if (hits.Count() > 0)
          foreach (var p in hits) {
            p.time = (int)reply.RoundtripTime;
            p.ttl = reply.Options.Ttl;
            if (p.fails < 0) {
              if (!(bool)Sticky.IsChecked)
                p.fails++;
            } else
              // if (!(bool)Sticky.IsChecked)
              p.fails = 0;
          } else {
          pingResults.Add(new PingResult(reply.Address, (int)reply.RoundtripTime, reply.Options.Ttl, gethost));
          graph.model.Add(pingResults.Count);
          graph.Plot1.InvalidatePlot(true);
        }
      } else {
        var hits = pingResults.Where(x => x.ip.ToString() == addr).FirstOrDefault();
        if (hits != null) {
            hits.fails++;
          if (hits.fails < 0)
            hits.fails = 1;
          if ((hits.fails > 10)) {
            if (!(bool)Sticky.IsChecked) {
              pingResults.Remove(hits);
              graph.model.Add(pingResults.Count);
              graph.Plot1.InvalidatePlot(true);
            }
            else
              hits.fails = 10;
          }
        }
        speed.model.Clean(Int32.Parse(addr.Substring(addr.LastIndexOf('.')+1)));
        //speed.Plot1.InvalidatePlot(true);

      }
    }


    private void PingButton_Click(object sender, RoutedEventArgs e) {
      //PingButton.IsEnabled = !continuous;
      continuous = !continuous;
      if (continuous)
        PingButton.Content = "Stop";
      else
        PingButton.Content = "Ping";

      if (!continuous)
        return;

      Results.Items.SortDescriptions.Add(new SortDescription(Results.Columns[0].SortMemberPath, ListSortDirection.Ascending));
      // Hide sort, ttl & color columns
      Results.Columns[0].Visibility = Visibility.Hidden;
      Results.Columns[2].Visibility = Visibility.Hidden;
      Results.Columns[7].Visibility = Visibility.Hidden;
      // Resize time & fails columns
      Results.Columns[3].Width = new DataGridLength(30);
      Results.Columns[6].Width = new DataGridLength(30);
      pingResults.Clear();
      graph.model.Clear();
      speed.model.Clear();
      for (int i = 0; i < 255; i++)
        speed.model.point[i].Clear();
      IPAddress temp;
      try {
        if (!IPAddress.TryParse(IPBox.Text, out temp))
          IPBox.Text = Dns.GetHostAddresses(IPBox.Text).FirstOrDefault().ToString();
      } catch (SocketException) { };

      try {
        for (int i = 1; i < 255; i++) {
          doPing(pingSender[i], IPBox.Text.Substring(0, IPBox.Text.LastIndexOf('.')) + "." + i);
          //Thread.Sleep(100);
        }
      }
      catch (InvalidOperationException) { }; // Catch error of ping already being sent
    }

    private void GetIPButton_Click(object sender, RoutedEventArgs e) {
      IPHostEntry host;
      host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (IPAddress ip in host.AddressList) {
        if (ip.AddressFamily == AddressFamily.InterNetwork) {
          IPBox.Text = ip.ToString();
          break;
        }
      }
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
      IPBox.Text = Results.SelectedItem.ToString();
    }


    private void CheckBox_Click_1(object sender, RoutedEventArgs e) {
      continuous = (bool)((CheckBox)sender).IsChecked;
      if (!continuous) PingButton.IsEnabled = true;
      //PingButton.IsEnabled = continuous;
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e) {
      gethost = (bool)((CheckBox)sender).IsChecked;
    }

    private void Window_Closing_1(object sender, CancelEventArgs e) {
      graph.Close();
      speed.Close();
    }

    private void GraphButton_Click(object sender, RoutedEventArgs e) {
      graph = new Graph();
      graph.Show();
    }

  }
}