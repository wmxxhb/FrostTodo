using System;
using System.Globalization;
using System.Linq;

namespace FrostTodo {
 public static class Recurrence {
  static DateTime Seed(Todo t,DateTime today){
   if(Logic.IsDate(t.RepeatDue))return Logic.Date(t.RepeatDue);if(Logic.IsDate(t.Due))return Logic.Date(t.Due);
   if(Logic.IsStamp(t.Reminder))return Logic.Stamp(t.Reminder).Date;
   DateTime created;if(DateTime.TryParse(t.Created,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out created))return (created.Kind==DateTimeKind.Utc?created.ToLocalTime():created).Date;
   return today.Date;
  }
  public static DateTime? Occurrence(Todo t,DateTime today){
   if(t.Repeat=="none")return Logic.IsDate(t.Due)?Logic.Date(t.Due):(DateTime?)null;
   DateTime seed=Seed(t,today),earliest=seed>today.Date?seed:today.Date;
   if(t.Repeat=="daily")return earliest;
   if(t.Repeat=="weekly"){int days=(earliest-seed).Days,add=(7-days%7)%7;return earliest<=DateTime.MaxValue.Date.AddDays(-add)?earliest.AddDays(add):(DateTime?)null;}
   if(t.Repeat=="monthly"){
    int anchor=t.Anchor>0?Math.Min(31,t.Anchor):seed.Day;DateTime current=new DateTime(earliest.Year,earliest.Month,Math.Min(anchor,DateTime.DaysInMonth(earliest.Year,earliest.Month)));
    if(current<earliest){if(earliest.Year==9999&&earliest.Month==12)return null;DateTime month=new DateTime(earliest.Year,earliest.Month,1).AddMonths(1);current=new DateTime(month.Year,month.Month,Math.Min(anchor,DateTime.DaysInMonth(month.Year,month.Month)));}return current;
   }
   for(int day=0;day<7&&earliest<=DateTime.MaxValue.Date.AddDays(-day);day++){DateTime current=earliest.AddDays(day);if(t.Repeat=="weekdays"&&current.DayOfWeek!=DayOfWeek.Saturday&&current.DayOfWeek!=DayOfWeek.Sunday)return current;if(t.Repeat=="custom"&&t.WeekDays!=null&&t.WeekDays.Contains((int)current.DayOfWeek))return current;}
   return null;
  }
  public static string DisplayDue(Todo t,DateTime today){DateTime? occurrence=Occurrence(t,today);return occurrence.HasValue?Logic.Ymd(occurrence.Value):"";}
  public static void ShiftReminder(Todo t,DateTime from,DateTime to){if(Logic.IsStamp(t.Reminder)&&from!=to){t.Reminder=Logic.Stamp(t.Reminder).Add(to-from).ToString("yyyy-MM-dd'T'HH:mm");t.ReminderFired=false;}}
  public static bool Refresh(Todo t,DateTime today){
   if(t.Done||t.Repeat=="none")return false;DateTime old=Seed(t,today);DateTime? occurrence=Occurrence(t,today);if(!occurrence.HasValue)return false;
   bool changed=false;if(t.Repeat=="monthly"&&t.Anchor<=0){t.Anchor=old.Day;changed=true;}string date=Logic.Ymd(occurrence.Value);
   if(t.RepeatDue!=date){t.RepeatDue=date;changed=true;}if(t.Due!=""&&t.Due!=date){t.Due=date;changed=true;}
   if(old!=occurrence.Value){ShiftReminder(t,old,occurrence.Value);changed=true;}return changed;
  }
  public static bool RefreshAll(AppData data,DateTime today){bool changed=false;foreach(Todo task in data.Tasks)changed=Refresh(task,today)||changed;return changed;}
  public static void Reset(Todo t,DateTime today){
   t.RepeatDue="";t.SnoozedUntil="";t.SnoozedFor="";if(t.Repeat=="none")return;
   DateTime first=Logic.IsDate(t.Due)?Logic.Date(t.Due):Logic.IsStamp(t.Reminder)?Logic.Stamp(t.Reminder).Date:today.Date;t.RepeatDue=Logic.Ymd(first);t.Anchor=first.Day;Refresh(t,today);
  }
  public static void MergeEditor(Todo draft,Todo baseline,Todo current){
   bool sameRule=draft.Repeat==baseline.Repeat&&draft.Due==baseline.Due&&draft.WeekDays.OrderBy(x=>x).SequenceEqual(baseline.WeekDays.OrderBy(x=>x));
   if(sameRule){draft.Due=current.Due;draft.RepeatDue=current.RepeatDue;draft.Anchor=current.Anchor;}
   if(draft.Reminder==baseline.Reminder){draft.Reminder=current.Reminder;draft.ReminderFired=current.ReminderFired;draft.SnoozedUntil=current.SnoozedUntil;draft.SnoozedFor=current.SnoozedFor;}
  }
  public static bool ValidTime(Todo t){return String.IsNullOrEmpty(t.Time)||Logic.IsTime(t.Time)&&(Logic.IsDate(t.Due)||t.Repeat!="none");}
  public static void MergeDeliveryState(Todo restored,Todo current,DateTime today){
   Refresh(restored,today);if(current==null||restored.Done!=current.Done||restored.Repeat!=current.Repeat||restored.RepeatDue!=current.RepeatDue||restored.Reminder!=current.Reminder)return;
   restored.ReminderFired=current.ReminderFired;restored.SnoozedUntil=current.SnoozedUntil;restored.SnoozedFor=current.SnoozedFor;
  }
  public static bool ValidReminder(Todo t,Todo original,DateTime now){return String.IsNullOrEmpty(t.Reminder)||Logic.IsStamp(t.Reminder)&&(original!=null&&t.Reminder==original.Reminder||Logic.Stamp(t.Reminder)>now);}
  static string Cycle(Todo t,DateTime today){return t.Repeat=="none"?"single":DisplayDue(t,today);}
  public static void Snooze(Todo t,DateTime now){Snooze(t,now,Cycle(t,now.Date));}
  public static void Snooze(Todo t,DateTime now,string cycle){t.SnoozedUntil=now.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm");t.SnoozedFor=cycle;}
  public static bool ReminderDue(Todo t,DateTime now){
   if(t.Done)return false;bool snoozed=Logic.IsStamp(t.SnoozedUntil),sameCycle=snoozed&&t.SnoozedFor==Cycle(t,now.Date);
   return snoozed&&Logic.Stamp(t.SnoozedUntil)<=now||!t.ReminderFired&&Logic.IsStamp(t.Reminder)&&Logic.Stamp(t.Reminder)<=now&&!sameCycle;
  }
  public static string FireReminder(Todo t,DateTime now){
   bool snoozed=Logic.IsStamp(t.SnoozedUntil),sameCycle=snoozed&&t.SnoozedFor==Cycle(t,now.Date),snoozeDue=snoozed&&Logic.Stamp(t.SnoozedUntil)<=now;
   bool currentAlarm=!sameCycle&&!t.ReminderFired&&Logic.IsStamp(t.Reminder)&&Logic.Stamp(t.Reminder)<=now||snoozeDue&&sameCycle;
   string notificationCycle=currentAlarm?Cycle(t,now.Date):t.SnoozedFor;if(currentAlarm)t.ReminderFired=true;
   if(snoozeDue){t.SnoozedUntil="";t.SnoozedFor="";}
   return notificationCycle;
  }
 }
}
