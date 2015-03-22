using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace Uber
{
    public static class ListViewHelper
    {
        public static void CopySelectedRowsToClipboard(this ListView listView)
        {
            var stringBuilder = new StringBuilder();

            foreach(var item in listView.SelectedItems)
            {
                var listViewItem = item as ListViewItem;
                if(listViewItem == null)
                {
                    continue;
                }

                var realItem = listViewItem.Content;
                if(realItem == null)
                {
                    continue;
                }

                stringBuilder.AppendLine(realItem.ToString());
            }

            var allRows = stringBuilder.ToString();
            var allRowsFixed = allRows.TrimEnd(new char[] { '\r', '\n' });

            Clipboard.SetDataObject(allRowsFixed, true);
        }
    }
}