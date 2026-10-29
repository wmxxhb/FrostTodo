using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FrostTodo {
 public sealed class Store {
  public readonly string DirectoryPath;public readonly string FilePath; readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=40*1024*1024};
  public string RecoveryMessage="";
  public Store(string path){DirectoryPath=path;FilePath=Path.Combine(path,"tasks.json");}
  public AppData Load(){
   Directory.CreateDirectory(DirectoryPath);if(!File.Exists(FilePath))return new AppData();
   try{return Decode(File.ReadAllText(FilePath,Encoding.UTF8));}catch(Exception first){
    string previous=FilePath+".previous";
    if(File.Exists(previous)){try{AppData data=Decode(File.ReadAllText(previous,Encoding.UTF8));string preserved=FilePath+".damaged-"+DateTime.Now.ToString("yyyyMMddHHmmss");File.Copy(FilePath,preserved,false);RecoveryMessage="主数据文件无法读取，已从上次保存恢复；原文件已保留。";return data;}catch(Exception){}}
    throw new InvalidOperationException("任务文件无法读取，原文件已保留。请从备份恢复。\n"+first.Message);
   }
  }
  public AppData Decode(string text){AppData d=json.Deserialize<AppData>(text);if(d==null)throw new InvalidOperationException("备份内容为空。");d.Normalize();return d;}
  public string Encode(AppData data){return json.Serialize(data);}
  public void Save(AppData data){
   Directory.CreateDirectory(DirectoryPath);string temp=FilePath+".writing";
   byte[] content=new UTF8Encoding(false).GetBytes(Encode(data));
   using(FileStream file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(content,0,content.Length);file.Flush(true);}
   if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".previous",true);else File.Move(temp,FilePath);
  }
  public void Export(AppData data,string path){File.WriteAllText(path,Encode(data),new UTF8Encoding(false));}
 }
}
