using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;

namespace FrostTodo {
 public sealed class TimePicker:Grid {
  readonly TextBox input=new TextBox{MaxLength=8,Height=Ui.InputHeight,MinHeight=Ui.InputHeight,MaxHeight=Ui.InputHeight,Padding=new Thickness(8,0,29,0),VerticalContentAlignment=VerticalAlignment.Center};
  readonly Popup popup=new Popup{AllowsTransparency=true,StaysOpen=false,Placement=PlacementMode.Bottom};
  public string Value{get{return Logic.NormalizeTime(input.Text);}set{input.Text=value??"";}}
  public TimePicker(string name,string value){
   Height=Ui.InputHeight;MinHeight=Ui.InputHeight;MaxHeight=Ui.InputHeight;VerticalAlignment=VerticalAlignment.Top;ClipToBounds=true;
   Value=value;AutomationProperties.SetName(input,name);Children.Add(input);
   TextBlock placeholder=Ui.Text("时:分",11,true);placeholder.Margin=new Thickness(9,0,30,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;Children.Add(placeholder);input.TextChanged+=delegate{placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;};placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
   Button clock=Ui.Icon("\uE917","选择"+name,Open);clock.Width=27;clock.Height=28;clock.HorizontalAlignment=HorizontalAlignment.Right;clock.VerticalAlignment=VerticalAlignment.Center;clock.Margin=new Thickness(0,0,2,0);clock.Opacity=.65;Children.Add(clock);popup.PlacementTarget=input;
   input.LostKeyboardFocus+=delegate{string normalized=Value;if(normalized==""||Logic.IsTime(normalized))input.Text=normalized;};
  }
  void Open(){
   DateTime now=DateTime.Now;string current=Value;int hour=Logic.IsTime(current)?Int32.Parse(current.Substring(0,2)):now.Hour,minute=Logic.IsTime(current)?Int32.Parse(current.Substring(3,2)):now.Minute;
   StackPanel panel=new StackPanel{Margin=new Thickness(12)};panel.Resources["Ink"]=Ui.Brush("#263344");panel.Resources["Muted"]=Ui.Brush("#66778B");panel.Resources["Field"]=Ui.Brush("#FFFFFFFF");
   string[] hours=Enumerable.Range(0,24).Select(i=>i.ToString("00")).ToArray(),minutes=Enumerable.Range(0,60).Select(i=>i.ToString("00")).ToArray();
   Grid selectors=new Grid();selectors.ColumnDefinitions.Add(new ColumnDefinition());selectors.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});selectors.ColumnDefinitions.Add(new ColumnDefinition());
   ComboBox h=Ui.Select(hours,hours,hour.ToString("00"),null),m=Ui.Select(minutes,minutes,minute.ToString("00"),null);AutomationProperties.SetName(h,"小时");AutomationProperties.SetName(m,"分钟");selectors.Children.Add(h);Grid.SetColumn(m,2);selectors.Children.Add(m);panel.Children.Add(selectors);
   Grid buttons=new Grid{Margin=new Thickness(0,10,0,0)};Button clear=Ui.Button("清除",delegate{Value="";popup.IsOpen=false;}),done=Ui.Button("确定",delegate{Value=Ui.Value(h)+":"+Ui.Value(m);popup.IsOpen=false;input.Focus();});clear.HorizontalAlignment=HorizontalAlignment.Left;done.HorizontalAlignment=HorizontalAlignment.Right;buttons.Children.Add(clear);buttons.Children.Add(done);panel.Children.Add(buttons);
   popup.Child=new Border{Background=Ui.Brush("#F2F5F9"),BorderBrush=Ui.Brush("#B9C8D8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Width=182,Child=panel};popup.IsOpen=true;
  }
 }
}
