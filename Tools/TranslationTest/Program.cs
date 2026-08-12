using LocalizationLibrary;

var manager = LocalizationManager.Current;
manager.Initialize();
string previousLanguage = manager.CurrentLanguage.Code;
manager.SetLanguage("zh-CN");

var checks = new Dictionary<string, string>
{
    ["Press a key, button or move an axis to bind 'Assist'"] = "按下按键、按钮或移动轴以绑定“Assist”",
    ["Download progress: 42%"] = "下载进度：42%",
    ["Successfully bound 'Assist' to Keyboard - N"] = "已成功将“Assist”绑定到 Keyboard - N",
    ["Current Speed"] = "当前速度",
    ["Speed Limit"] = "限速",
    ["off"] = "关闭",
    ["on"] = "开启",
    ["Off"] = "关闭",
    ["On"] = "开启",
    ["Match Game"] = "匹配游戏",
    ["Overlay Interaction"] = "覆盖层交互",
    ["Couldn't connect to the game. Please open ETS2 or ATS and enable the SDK."] = "无法连接到游戏。请打开 ETS2 或 ATS 并启用 SDK。",
};

var failures = checks
    .Where(pair => manager.Translate(pair.Key) != pair.Value)
    .Select(pair => $"{pair.Key} -> {manager.Translate(pair.Key)}")
    .ToList();

manager.SetLanguage(previousLanguage);

if (failures.Count > 0)
{
    foreach (var failure in failures)
        Console.WriteLine($"FAIL: {failure}");
    return 1;
}

Console.WriteLine("ALL PASSED");
return 0;
