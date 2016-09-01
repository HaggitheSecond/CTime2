﻿using System.Linq;
using Windows.UI.Xaml.Controls;

namespace CTime2.Views.Statistics.DetailedStatistic
{
    public sealed partial class OverTime : Page
    {
        public DetailedStatisticViewModel ViewModel => this.DataContext as DetailedStatisticViewModel;

        public OverTime()
        {
            this.InitializeComponent();
        }

        private void DataPointSeries_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ViewModel.NavigateTo((StatisticChartItem)e.AddedItems.First());
        }
    }
}
