namespace FrostTodo {
 public static class DesktopAppearance {
  public static void ResetText(Preferences p){p.Font="desktop";p.TextSize=16;p.TabTextSize=0;p.TextWeight="normal";p.TextRendering="desktop";p.TextContrast=100;p.TextSpacing=0;p.TextLineGap=4;}
  public static void Reset(Preferences p){ResetText(p);p.GptTheme="system";p.Tint="#FFFFFF";p.Foreground="auto";p.Opacity=100;p.Density="balanced";p.BackgroundImage="";p.BackgroundImageName="";}
 }
}
