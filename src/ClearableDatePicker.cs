using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FrostTodo {
 public sealed class ClearableDatePicker:DatePicker {
  Button clear;
  public ClearableDatePicker(){
   SetResourceReference(StyleProperty,typeof(DatePicker));Height=Ui.InputHeight;MinHeight=Ui.InputHeight;MaxHeight=Ui.InputHeight;VerticalAlignment=VerticalAlignment.Top;
   SelectedDateChanged+=delegate{UpdateClear();};CalendarOpened+=delegate{
    Popup popup=GetTemplateChild("PART_Popup") as Popup;if(popup==null||!(popup.Child is Calendar))return;
    Calendar calendar=(Calendar)popup.Child;if(UiVariant.IsGpt)GptTheme.StyleCalendar(calendar);popup.Child=null;StackPanel panel=new StackPanel();if(!UiVariant.IsGpt){panel.Resources["Ink"]=Ui.Brush("#263344");panel.Resources["Muted"]=Ui.Brush("#66778B");}panel.Children.Add(calendar);
    Button reset=Ui.Button("清除日期",delegate{ClearDate();IsDropDownOpen=false;});reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(7,3,7,7);panel.Children.Add(reset);
    popup.Child=new Border{Background=Ui.Brush("#F3F6FA"),CornerRadius=new CornerRadius(10),Child=panel};GptTheme.Popup((Border)popup.Child);
   };
  }
  public override void OnApplyTemplate(){if(clear!=null)clear.Click-=ClearClicked;base.OnApplyTemplate();clear=GetTemplateChild("PART_ClearButton") as Button;if(clear!=null){clear.Click+=ClearClicked;AutomationProperties.SetName(clear,"清除"+(AutomationProperties.GetName(this)??"日期"));}UpdateClear();}
  void UpdateClear(){if(clear!=null)clear.Visibility=SelectedDate.HasValue?Visibility.Visible:Visibility.Hidden;}
  void ClearClicked(object sender,RoutedEventArgs e){ClearDate();e.Handled=true;}
  public void ClearDate(){SelectedDate=null;SetCurrentValue(TextProperty,String.Empty);UpdateClear();}
 }
}
