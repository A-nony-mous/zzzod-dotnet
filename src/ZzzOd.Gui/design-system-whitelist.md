# GUI 设计体系审计白名单

每项格式为 `规则 | 文件相对路径 | 行内定位子串 | 理由`。审计会拒绝已经失效的条目。

R1 | Views/FrontierPages/WorldPatrol/FrontierWorldPatrolImageViewer.axaml | Rectangle Stroke="#B4FF0000" | 路线录制选框必须在任意游戏画面上保持固定红色辨识度
R1 | Views/FrontierPages/Home/FrontierHomePage.axaml.cs | luminance >= 160 | 首页按钮前景由用户媒体主题色的实时亮度计算，不能映射为固定主题画刷
R1 | Views/FrontierPages/Home/FrontierHomePage.axaml.cs | new(Colors.Transparent) | 用户媒体主题色按钮的动态交互画刷需要透明边框，不承载主题色值
R1 | Views/FrontierPages/Home/FrontierHomePage.axaml.cs | Color.FromRgb(red, green, blue) | 从已保存的用户 RGB 配置还原主题色，数值来自用户配置
R1 | Views/FrontierPages/Settings/FrontierCustomSettingsPage.axaml.cs | Color.FromRgb(r, g, b) | 将用户在主题色对话框输入的 RGB 值应用为系统强调色
