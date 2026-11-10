using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Models
{
    public class MenuItemModel
    {
        public string Header { get; set; } = string.Empty;    // 显示文本
        public string ViewName { get; set; } = string.Empty;  // 导航目标 View 名称
    }
}
