using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace Uber
{
    public static class WpfHelper
    {
        public static FrameworkElement CreateDualColumnPanel(IEnumerable<Tuple<FrameworkElement, FrameworkElement>> elementPairs, int width, int dy = 2, int dx = 5)
        {
            var rootPanel = new StackPanel();
            rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            rootPanel.VerticalAlignment = VerticalAlignment.Stretch;
            rootPanel.Margin = new Thickness(5);
            rootPanel.Orientation = Orientation.Vertical;

            foreach(var elementPair in elementPairs)
            {
                var a = elementPair.Item1;
                var b = elementPair.Item2;
                a.Width = width;

                var pairPanel = new StackPanel();
                pairPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                pairPanel.VerticalAlignment = VerticalAlignment.Top;
                pairPanel.Margin = new Thickness(dx, dy, dx, dy);
                pairPanel.Orientation = Orientation.Horizontal;
                pairPanel.Children.Add(a);
                pairPanel.Children.Add(b);

                rootPanel.Children.Add(pairPanel);
            }
            
            return rootPanel;
        }

        public static FrameworkElement CreateRow(FrameworkElement a, FrameworkElement b, int width, int dy = 2, int dx = 5)
        {
            a.Width = width;

            var pairPanel = new StackPanel();
            pairPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            pairPanel.VerticalAlignment = VerticalAlignment.Top;
            pairPanel.Margin = new Thickness(dx, dy, dx, dy);
            pairPanel.Orientation = Orientation.Horizontal;
            pairPanel.Children.Add(a);
            pairPanel.Children.Add(b);

            return pairPanel;
        }

        public static FrameworkElement CreateRow(List<FrameworkElement> elements, int width, int dy = 2, int dx = 5)
        {
            if(elements.Count == 0)
            {
                return new TextBlock { Text = "woops", Margin = new Thickness(dx, dy, dx, dy) };
            }

            if(elements.Count == 1)
            {
                elements[0].Margin = new Thickness(dx, dy, dx, dy);
                return elements[0];
            }

            elements[0].Width = width;

            var pairPanel = new StackPanel();
            pairPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            pairPanel.VerticalAlignment = VerticalAlignment.Top;
            pairPanel.Margin = new Thickness(dx, dy, dx, dy);
            pairPanel.Orientation = Orientation.Horizontal;
            foreach(var element in elements)
            {
                pairPanel.Children.Add(element);
            }

            return pairPanel;
        }

        public static Tuple<FrameworkElement, FrameworkElement> CreateTuple(string a, FrameworkElement b)
        {
            return new Tuple<FrameworkElement, FrameworkElement>(new Label { Content = a }, b);
        }

        public static Tuple<FrameworkElement, FrameworkElement> CreateTuple(string a, string b)
        {
            return new Tuple<FrameworkElement, FrameworkElement>(new Label { Content = a }, new Label { Content = b });
        }
    }
}