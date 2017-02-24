﻿/* MultiPing by Amund Børsand (amund.borsand@nov.com)
 * march 2015
 * Idea by Jan Øyvind Lyche */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

namespace MultiPing {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  /// 


  public partial class MainWindow : Window {

    Ping[] pingSender = new Ping[256];
    static bool continuous = false;
    static bool getmac = true;
    public static string manuf;
    public static MainWindow mainWin;
    public static Dispatcher disp;
    public static bool mappingtheworld;

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

    }


    public async void doPing(Ping pingSender, string who) {
      int timeout = 1000;
      try {
        pingSender.PingCompleted += new PingCompletedEventHandler(PingCompletedCallback);
        pingSender.SendAsync(who, timeout, who);
      } catch (PingException ex) {
        Console.WriteLine(who + " "+ex.ToString());
      } catch (InvalidOperationException ex) {
        Console.WriteLine(who + " "+ex.ToString());
        await Task.Delay(2000);
        pingSender.SendAsyncCancel();
      } //Thread.Sleep(2000); pingSender.SendAsync(who, timeout, who); }
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
        if (speed.IsVisible) {
          speed.model.Add(reply.Address.GetAddressBytes()[3], reply.RoundtripTime);
          //speed.model.line[reply.Address.GetAddressBytes()[3]].notify reply.Plot1.InvalidatePlot(true);
          speed.Plot1.InvalidatePlot(true);
        }
        var hits = PingResults.Where(x => x.ip.Equals(reply.Address));
        if (hits.Count() > 0)
          foreach (var p in hits) {
            p.time = (int)reply.RoundtripTime;
            p.ttl = reply.Options.Ttl;
            if (speed.IsVisible) {
              speed.Plot1.Axes.First().Minimum = // set left window edge, to give better scrolling
              speed.model.lasttime - 0.00075;
              //speed.Plot1.Axes[1].Minimum = 100;
            }
            if (p.fails < 0) {
              if (!(bool)Sticky.IsChecked)
                p.fails++;
            } else
              // if (!(bool)Sticky.IsChecked)
              p.fails = 0;
          } else { // if new row
          pingResults.Add(new PingResult(reply.Address, (int)reply.RoundtripTime, reply.Options.Ttl, getmac, (bool)benchMarkCheckBox.IsChecked));
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
            } else
              hits.fails = 10;
          }
        }
        //OxyPlot.Axes.DateTimeAxis.ToDouble(new TimeSpan(0, 0, 10));
        //speed.model.Clean(int.Parse(addr.Substring(addr.LastIndexOf('.') + 1)));
        //speed.Plot1.InvalidatePlot(true);
        //Results.ScrollIntoView(Results.Items[Results.Items.Count - 1]);
      }
    }


    private void PingButton_Click(object sender, RoutedEventArgs e) {
      //PingButton.IsEnabled = !continuous;
      mappingtheworld = false;
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
          foreach (var addr in Dns.GetHostAddresses(IPBox.Text))
            if (addr.AddressFamily == AddressFamily.InterNetwork) {
              IPBox.Text = addr.ToString();
              break;
            }
      } catch (SocketException) { };

      try {
        for (int i = 1; i < 255; i++) {
          doPing(pingSender[i], IPBox.Text.Substring(0, IPBox.Text.LastIndexOf('.')) + "." + i);
          //await Task.Delay(10);
          //Thread.Sleep(1000);
        }
      }
      catch (InvalidOperationException) { Console.WriteLine("Failed in button"); }; // Catch error of ping already being sent
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
      getmac = (bool)((CheckBox)sender).IsChecked;
    }

    private void Window_Closing_1(object sender, CancelEventArgs e) {
      graph.Close();
      speed.Close();
    }

    private void GraphButton_Click(object sender, RoutedEventArgs e) {
      graph = new Graph();
      graph.Show();
    }

    private void button_Click(object sender, RoutedEventArgs e) {
      //PingButton.IsEnabled = !continuous;
      continuous = false;
      getmac = false;
      GetMacCheckBox.IsChecked = false;
      mappingtheworld = !mappingtheworld;
      if (mappingtheworld)
        MapTheWorldButton.Content = "Stop";
      else
        MapTheWorldButton.Content = "Map the world";

      if (!mappingtheworld)
        return;

      Results.Items.SortDescriptions.Clear();
      Results.Items.SortDescriptions.Add(new SortDescription(Results.Columns[0].SortMemberPath, ListSortDirection.Descending));
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
          foreach (var addr in Dns.GetHostAddresses(IPBox.Text))
            if (addr.AddressFamily == AddressFamily.InterNetwork) {
              IPBox.Text = addr.ToString();
              break;
            }
      }
      catch (SocketException) { };

      try {
        IPAddress ip = IPAddress.Parse(IPBox.Text);
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(async () => {
        byte lstart = ip.GetAddressBytes()[0];
        byte kstart = ip.GetAddressBytes()[1];
        byte jstart = ip.GetAddressBytes()[2];
          for (byte l = lstart; l < 255; l++) {
            for (byte k = kstart; k < 255; k++) {
              for (byte j = jstart; j < 255; j++) {
                IPBox.Text = new IPAddress(new byte[] { l, k, j, 1 }).ToString();
                if (!mappingtheworld)
                  return;
                for (byte i = 1; i < 255; i++) {
                  IPAddress curr = new IPAddress(new byte[] { l, k, j, i });
                  doPing(pingSender[i], curr.ToString());
                }
                await Task.Delay(1000);
                if (j == 254)
                  jstart = 1;
              }
              if (k == 254)
                kstart = 1;
            }
            if (l == 254)
              lstart = 1;
          }
        }));
      }
      catch (InvalidOperationException) { Console.WriteLine("Failed in button"); }; // Catch error of ping already being sent
    }
  }
}
