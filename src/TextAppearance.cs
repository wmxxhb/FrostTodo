using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FrostTodo {
 public static class TextAppearance {
  public static void Reset(Preferences p){p.Font=UiVariant.IsGpt?"segoe":"system";p.TextSize=15;p.TextWeight="normal";p.TextRendering="sharp";p.TextContrast=UiVariant.IsGpt?100:76;p.TextSpacing=0;p.TextLineGap=4;}
  public static void Recommended(Preferences p){Reset(p);p.Font="yahei";p.TextSize=16;p.TextWeight="medium";p.TextContrast=100;p.TextLineGap=6;}
  public static void Normalize(Preferences p){
   p.TextSize=Math.Max(12,Math.Min(22,p.TextSize));p.TextContrast=Math.Max(50,Math.Min(100,p.TextContrast));p.TextLineGap=Math.Max(0,Math.Min(16,p.TextLineGap));
   if(!new[]{"normal","medium","semibold","bold"}.Contains(p.TextWeight))p.TextWeight="normal";
   if(!new[]{"sharp","soft","auto","pixel"}.Contains(p.TextRendering))p.TextRendering="sharp";
   p.TextSpacing=Double.IsNaN(p.TextSpacing)||Double.IsInfinity(p.TextSpacing)?0:Math.Max(0,Math.Min(3,p.TextSpacing));
  }
  public static string SizeKey(double size){return "UiSize"+size.ToString("0.#",CultureInfo.InvariantCulture);}
  public static void BindSize(FrameworkElement element,double size){element.SetResourceReference(element is TextBlock?TextBlock.FontSizeProperty:Control.FontSizeProperty,SizeKey(size));}
  public static void Publish(Preferences p){
   if(Application.Current==null)return;ResourceDictionary resources=Application.Current.Resources;
   for(double size=10;size<=24;size+=.5)resources[SizeKey(size)]=Math.Round(size*p.TextSize/15.0,2);
   FontWeight weight=p.TextWeight=="bold"?FontWeights.Bold:p.TextWeight=="semibold"?FontWeights.SemiBold:p.TextWeight=="medium"?FontWeights.Medium:FontWeights.Normal;
   resources["UiWeight"]=weight;resources["UiEmphasisWeight"]=weight.ToOpenTypeWeight()<500?FontWeights.Medium:weight;
   resources["UiFormatting"]=p.TextRendering=="soft"?TextFormattingMode.Ideal:TextFormattingMode.Display;
   resources["UiRendering"]=p.TextRendering=="auto"?TextRenderingMode.Auto:p.TextRendering=="pixel"?TextRenderingMode.Aliased:TextRenderingMode.Grayscale;
   resources["TaskCharacterSpacing"]=p.TextSpacing;resources["TaskMetaGap"]=new Thickness(0,p.TextLineGap,0,0);resources["TaskTextPadding"]=new Thickness(1,6+Math.Max(0,p.TextLineGap-4)/2.0,2,6+Math.Max(0,p.TextLineGap-4)/2.0);
   resources["UiTabsHeight"]=Math.Max(34,Math.Ceiling(11.5*p.TextSize/15.0*1.5)+16);resources["UiHeaderHeight"]=Math.Max(25,Math.Ceiling(p.TextSize*1.5));if(UiVariant.IsGpt)GptTheme.Publish(p);
  }
  public static void Configure(FrameworkElement element){element.SetResourceReference(TextOptions.TextFormattingModeProperty,"UiFormatting");element.SetResourceReference(TextOptions.TextRenderingModeProperty,"UiRendering");TextOptions.SetTextHintingMode(element,TextHintingMode.Fixed);}
  public static SpacedTitle Title(string text){SpacedTitle block=new SpacedTitle{Text=text,ToolTip=text,Focusable=false};BindSize(block,15);block.SetResourceReference(Control.FontFamilyProperty,"UiFont");block.SetResourceReference(Control.FontWeightProperty,"UiWeight");block.SetResourceReference(Control.ForegroundProperty,"Ink");block.SetResourceReference(SpacedTitle.SpacingProperty,"TaskCharacterSpacing");Configure(block);return block;}
 }
 // Zero tracking shapes the entire heading, preserving normal kerning and ligatures.
 public sealed class SpacedTitle:Control {
  public static readonly DependencyProperty TextProperty=DependencyProperty.Register("Text",typeof(string),typeof(SpacedTitle),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
  public string Text{get{return (string)GetValue(TextProperty);}set{SetValue(TextProperty,value);}}
  public TextTrimming TextTrimming{get;set;}
  public static readonly DependencyProperty TextDecorationsProperty=DependencyProperty.Register("TextDecorations",typeof(TextDecorationCollection),typeof(SpacedTitle),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
  public TextDecorationCollection TextDecorations{get{return (TextDecorationCollection)GetValue(TextDecorationsProperty);}set{SetValue(TextDecorationsProperty,value);}}
  public SpacedTitle(){TextTrimming=TextTrimming.CharacterEllipsis;}
  protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer(){return new SpacedTitlePeer(this);}
  public static readonly DependencyProperty SpacingProperty=DependencyProperty.Register("Spacing",typeof(double),typeof(SpacedTitle),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
  public double Spacing{get{return (double)GetValue(SpacingProperty);}set{SetValue(SpacingProperty,value);}}
  FormattedText Shape(string text){return new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection,new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),FontSize,Foreground??Brushes.Black,null,TextOptions.GetTextFormattingMode(this),VisualTreeHelper.GetDpi(this).PixelsPerDip);}
  string[] Elements(){System.Collections.Generic.List<string> elements=new System.Collections.Generic.List<string>();TextElementEnumerator cursor=StringInfo.GetTextElementEnumerator(Text??"");while(cursor.MoveNext())elements.Add(cursor.GetTextElement());return elements.ToArray();}
  protected override Size MeasureOverride(Size available){
   if(Spacing<=0){FormattedText text=Shape(Text??"");return new Size(Math.Min(available.Width,text.WidthIncludingTrailingWhitespace),text.Height);}double width=0,height=Shape("待").Height;string[] elements=Elements();foreach(string s in elements){FormattedText text=Shape(s);width+=text.WidthIncludingTrailingWhitespace;height=Math.Max(height,text.Height);}width+=Math.Max(0,elements.Length-1)*Spacing;return new Size(Math.Min(available.Width,width),height);
  }
  protected override void OnRender(DrawingContext dc){
   if(ActualWidth<=0||ActualHeight<=0)return;
   if(Spacing<=0){FormattedText text=Shape(Text??"");text.MaxTextWidth=ActualWidth;text.MaxLineCount=1;text.Trimming=TextTrimming;if(TextDecorations!=null)text.SetTextDecorations(TextDecorations);dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));dc.DrawText(text,new Point(0,0));dc.Pop();return;}double width=ActualWidth,x=0;string[] elements=Elements();FormattedText[] shaped=elements.Select(Shape).ToArray();double total=shaped.Sum(s=>s.WidthIncludingTrailingWhitespace)+Math.Max(0,shaped.Length-1)*Spacing;FormattedText ellipsis=Shape("…");bool trim=total>width&&TextTrimming!=TextTrimming.None;
   dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
   for(int i=0;i<shaped.Length;i++){FormattedText item=shaped[i];if(trim&&x+item.WidthIncludingTrailingWhitespace+ellipsis.WidthIncludingTrailingWhitespace>width){dc.DrawText(ellipsis,new Point(x,0));x+=ellipsis.WidthIncludingTrailingWhitespace;break;}dc.DrawText(item,new Point(x,0));x+=item.WidthIncludingTrailingWhitespace+Spacing;}
   if(TextDecorations!=null&&TextDecorations.Any(d=>d.Location==TextDecorationLocation.Strikethrough))dc.DrawLine(new Pen(Foreground,Math.Max(1,FontSize/15)),new Point(0,ActualHeight*.5),new Point(Math.Min(width,x),ActualHeight*.5));dc.Pop();
  }
 }
 sealed class SpacedTitlePeer:System.Windows.Automation.Peers.FrameworkElementAutomationPeer {
  public SpacedTitlePeer(SpacedTitle owner):base(owner){}
  protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore(){return System.Windows.Automation.Peers.AutomationControlType.Text;}
  protected override string GetClassNameCore(){return "TextBlock";}
  protected override string GetNameCore(){return ((SpacedTitle)Owner).Text??"";}
 }
}
