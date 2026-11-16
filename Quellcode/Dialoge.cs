// Fensterpositionen wiederherstellen – Info-Fenster und Dialog „Programm hinzufügen“
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Drawing.Drawing2D;

namespace Fensterpositionen {
public class AboutForm:FixedSizeForm {
 public AboutForm(Bitmap logo,Icon icon,EventHandler openBackups,UpdateHooks updates) {
  Text="Info über "+AppInfo.Product;
  // Normaler Rahmen wie die Hauptfenster (Icon mit gleichem Abstand), Größe fest: siehe FixedSizeForm.
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(540),pad=Ui.Px(32),y=Ui.Px(16);
  if(logo!=null) {
   Controls.Add(new LogoView{Logo=logo,BackColor=Ui.Bg,Bounds=new Rectangle(pad,y,w-2*pad,Ui.Px(150))});
   y+=Ui.Px(158);
  }
  Controls.Add(new Label{Text="Fensterpositionen wiederherstellen",Font=Ui.HeadingFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(34))});
  y+=Ui.Px(34);
  Controls.Add(new Label{Text="Version "+AppInfo.Version,Font=Ui.SmallFont,ForeColor=Ui.Accent,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(24))});
  y+=Ui.Px(36);
  string built="";int year=DateTime.Now.Year;
  try{DateTime stamp=System.IO.File.GetLastWriteTime(Application.ExecutablePath);built=stamp.ToString("dd.MM.yyyy");year=stamp.Year;}catch{}
  string root=Store.Root;
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  if(root.StartsWith(local,StringComparison.OrdinalIgnoreCase))root="%LOCALAPPDATA%"+root.Substring(local.Length);
  var lines=new[]{new[]{"Entwickler","Günther Schoder"},new[]{"Erstellt",built},new[]{"Farbschema",(Ui.Dark?"dunkel":"hell")+" (folgt Windows)"},new[]{"Positionen",root}};
  // Updates: "Updates" (49), "Automatisch prüfen" (32) und ggf. "Automatisch aktualisieren" (32).
  int lineH=Ui.Px(26),updateH=updates!=null?Ui.Px(49)+Ui.Px(32)+(updates.AutoInstall!=null?Ui.Px(32):0):0,cardH=lines.Length*lineH+Ui.Px(24)+updateH;
  var card=new Card{Bounds=new Rectangle(pad,y,w-2*pad,cardH),Padding=new Padding(0)};
  for(int i=0;i<lines.Length;i++) {
   card.Controls.Add(new Label{Text=lines[i][0],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),Ui.Px(12)+i*lineH,Ui.Px(96),lineH)});
   card.Controls.Add(new Label{Text=lines[i][1],Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(112),Ui.Px(12)+i*lineH,card.Width-Ui.Px(124),lineH)});
  }
  if(updates!=null)AddUpdateRows(card,Ui.Px(12)+lines.Length*lineH,updates);
  Controls.Add(card);
  y+=cardH+Ui.Px(18);
  string guide=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Anleitung.txt");
  var guideButton=new ModernButton("Anleitung",ButtonKind.Secondary,"\uE8A5");
  guideButton.Enabled=System.IO.File.Exists(guide);
  guideButton.Click+=(s,e)=>{try{Process.Start(guide);}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
  var backupButton=new ModernButton("Sicherungen",ButtonKind.Secondary,"\uE838");
  backupButton.Click+=openBackups;
  var traceButton=new ModernButton("Protokolle",ButtonKind.Secondary,"\uE81C");
  traceButton.Click+=(s,e)=>{
   try{Directory.CreateDirectory(TraceLog.Folder);Process.Start("explorer.exe","\""+TraceLog.Folder+"\"");}
   catch(Exception ex){MessageBox.Show(this,ex.Message);}
  };
  var closeButton=new ModernButton("Schließen",ButtonKind.Primary,null);
  closeButton.DialogResult=DialogResult.OK;
  var buttons=new[]{guideButton,backupButton,traceButton,closeButton};
  int total=0;foreach(var b in buttons)total+=b.Width;total+=Ui.Px(8)*(buttons.Length-1);
  int bx=(w-total)/2;
  foreach(var b in buttons){b.Location=new Point(bx,y);bx+=b.Width+Ui.Px(8);Controls.Add(b);}
  AcceptButton=closeButton;CancelButton=closeButton;
  y+=Ui.Px(38)+Ui.Px(16);
  Controls.Add(new Label{Text="© "+year+" Günther Schoder",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(22))});
  y+=Ui.Px(22)+Ui.Px(18);
  ClientSize=new Size(w,y);
 }
 // Updates (Globale Richtlinien, Abschnitt "Updates" › Anzeige; Referenz: Wake on LAN ab v1.65, AboutDialog.AddUpdateRows):
 // Zeile "Updates" im Raster der Zeilen darüber (Symbol, Überschrift, kurze zweite Zeile, bei Fehlern aufklappbare
 // "Details"), Knopf rechts neben beiden Zeilen; zwei Zeilenabstände tiefer "Automatisch prüfen", darunter
 // "Automatisch aktualisieren". uy = Beginn der Zeile, X0 = Beginn der Werte.
 static Font titleFont,detailFont,smallGlyphFont;
 GlyphView statusGlyph;Label statusText,statusDetail,moreLink;ModernButton statusButton;
 Panel moreBox;TextBox moreText;
 UpdateHooks hooks;Card updateCard;
 readonly List<Control> belowDetails=new List<Control>();
 ToolTip updateTips;
 int uy,boxHeight;
 string moreValue="";
 bool moreOpen;
 enum ButtonMode {Check,Install,Page}
 ButtonMode mode;
 int X0{get{return Ui.Px(112);}}
 int ValuesRight{get{return updateCard.Width-Ui.Px(16);}}
 const TextFormatFlags OneLine=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
 void AddUpdateRows(Card card,int top,UpdateHooks updates) {
  hooks=updates;updateCard=card;uy=top;
  if(titleFont==null)titleFont=new Font("Segoe UI",9.5f);
  if(detailFont==null)detailFont=new Font("Segoe UI",8.75f);
  if(smallGlyphFont==null&&Ui.IconFont!=null)smallGlyphFont=new Font(Ui.IconFont.FontFamily,10f);
  updateTips=new ModernToolTip();
  card.Controls.Add(new Label{Text="Updates",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),uy+Ui.Px(2),Ui.Px(96),Ui.Px(22))});
  statusGlyph=new GlyphView{BackColor=Ui.Surface,Bounds=new Rectangle(X0,uy+Ui.Px(2),Ui.Px(20),Ui.Px(22))};
  statusText=new Label{Font=titleFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft};
  statusDetail=new Label{Font=detailFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.TopLeft};
  moreLink=new Label{Text="Details",Font=detailFont,ForeColor=Ui.Accent,BackColor=Ui.Surface,UseMnemonic=false,TextAlign=ContentAlignment.TopLeft,Cursor=Cursors.Hand,Visible=false};
  moreLink.Click+=(s,e)=>SetDetailsOpen(!moreOpen);
  // Aufgeklapptes Feld mit dem technischen Originaltext (markier- und kopierbar), kein Pop-up.
  moreBox=new Panel{BackColor=Ui.Bg,Visible=false,Padding=new Padding(Ui.Px(8),Ui.Px(6),Ui.Px(8),Ui.Px(6))};
  moreText=new TextBox{Multiline=true,ReadOnly=true,WordWrap=true,BorderStyle=BorderStyle.None,BackColor=Ui.Bg,ForeColor=Ui.TextColor,Font=detailFont,Dock=DockStyle.Fill,TabStop=false};
  moreBox.Controls.Add(moreText);
  statusButton=new ModernButton("Jetzt prüfen",ButtonKind.Secondary,""){Height=Ui.Px(30),Margin=new Padding(0),GlyphFont=smallGlyphFont};
  statusButton.Click+=async(s,e)=>await StatusButtonClick();
  foreach(Control c in new Control[]{statusGlyph,statusText,statusDetail,moreLink,moreBox,statusButton})card.Controls.Add(c);
  statusButton.BringToFront();
  // "Automatisch prüfen": Text ab X0, Schalter rechtsbündig; Klick auf den Text schaltet um.
  int autoTop=uy+Ui.Px(49);
  var toggle=new ToggleSwitch{Checked=updates.Auto(),BackColor=Ui.Surface};
  toggle.Location=new Point(ValuesRight-toggle.Width,autoTop+(Ui.Px(32)-toggle.Height)/2);
  toggle.CheckedChanged+=(s,e)=>updates.SetAuto(toggle.Checked);
  var autoText=new Label{Text="Automatisch prüfen",Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,Cursor=Cursors.Hand,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Bounds=new Rectangle(X0,autoTop,toggle.Left-Ui.Px(8)-X0,Ui.Px(32))};
  autoText.Click+=(s,e)=>toggle.Checked=!toggle.Checked;
  card.Controls.Add(autoText);card.Controls.Add(toggle);
  belowDetails.Add(autoText);belowDetails.Add(toggle);
  // "Automatisch aktualisieren": direkt darunter, nur bedienbar, wenn "Automatisch prüfen" an ist.
  if(updates.AutoInstall!=null) {
   int instTop=autoTop+Ui.Px(32);
   var inst=new ToggleSwitch{Checked=updates.AutoInstall(),BackColor=Ui.Surface};
   inst.Location=new Point(ValuesRight-inst.Width,instTop+(Ui.Px(32)-inst.Height)/2);
   inst.CheckedChanged+=(s,e)=>{if(updates.SetAutoInstall!=null)updates.SetAutoInstall(inst.Checked);};
   var instText=new Label{Text="Automatisch aktualisieren",Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Bounds=new Rectangle(X0,instTop,inst.Left-Ui.Px(8)-X0,Ui.Px(32))};
   instText.Click+=(s,e)=>{if(inst.Enabled)inst.Checked=!inst.Checked;};
   string tip="Eine neue Version ohne Rückfrage installieren, sobald "+AppInfo.Product+" nicht beschäftigt ist. "+AppInfo.Product+" startet danach von selbst neu.";
   updateTips.SetToolTip(inst,tip);updateTips.SetToolTip(instText,tip);
   Action sync=()=>{
    inst.Enabled=toggle.Checked;
    instText.ForeColor=toggle.Checked?Ui.TextColor:Ui.Blend(Ui.Muted,Ui.Surface,0.4f);
    instText.Cursor=toggle.Checked?Cursors.Hand:Cursors.Default;
   };
   toggle.CheckedChanged+=(s,e)=>sync();
   sync();
   card.Controls.Add(instText);card.Controls.Add(inst);
   belowDetails.Add(instText);belowDetails.Add(inst);
  }
  ShowStatus(updates.Status());
 }
 // Knopf rechtsbündig neben beiden Zeilen (Oberkante uy + 8); zweite Zeile endet vor dem Knopf,
 // zu lange Texte mit "…" gekürzt (ganzer Text im Tooltip); "Details" etwa ein Leerzeichen hinter dem Text.
 void LayoutStatus() {
  statusButton.Location=new Point(ValuesRight-statusButton.Width,uy+Ui.Px(8));
  int tx=X0+Ui.Px(23);
  int limit=Math.Max(Ui.Px(40),statusButton.Left-Ui.Px(6)-tx);
  statusText.SetBounds(tx,uy+Ui.Px(2),limit,Ui.Px(22));
  int linkW=moreLink.Visible?TextRenderer.MeasureText(moreLink.Text,detailFont,Size.Empty,OneLine).Width+Ui.Px(2):0;
  int textW=statusDetail.Text.Length==0?0:TextRenderer.MeasureText(statusDetail.Text,detailFont,Size.Empty,OneLine).Width;
  int room=(moreLink.Visible?limit-linkW-Ui.Px(3):limit)-Ui.Px(8);
  if(textW>room)textW=Math.Max(0,room);
  statusDetail.SetBounds(tx,uy+Ui.Px(23),Math.Max(1,textW+Ui.Px(8)),Ui.Px(20));
  int mx=statusDetail.Text.Length==0?tx:tx+textW+Ui.Px(3);
  moreLink.SetBounds(mx,uy+Ui.Px(23),linkW,Ui.Px(20));
  moreBox.SetBounds(X0,uy+Ui.Px(46),ValuesRight-X0,Math.Max(0,boxHeight-Ui.Px(8)));
 }
 // "Details" auf- bzw. zuklappen: höchstens sechs Zeilen (sonst Bildlauf im Feld); die Zeilen darunter,
 // die Karte, alles darunter und das Fenster rücken um Feldhöhe + 8.
 void SetDetailsOpen(bool on) {
  int want=0;
  if(on&&moreValue.Length>0) {
   int width=Math.Max(Ui.Px(40),ValuesRight-X0-Ui.Px(16));
   int line=TextRenderer.MeasureText("Ag",detailFont).Height;
   int h=TextRenderer.MeasureText(moreValue,detailFont,new Size(width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
   bool scroll=h>line*6;
   h=Math.Min(h,line*6);
   moreText.ScrollBars=scroll?ScrollBars.Vertical:ScrollBars.None;
   want=h+Ui.Px(12)+Ui.Px(8);
  } else on=false;
  moreOpen=on;
  moreText.Text=moreValue;
  moreLink.Text=on?"Weniger":"Details";
  moreBox.Visible=on;
  int d=want-boxHeight;
  boxHeight=want;
  foreach(var c in belowDetails)c.Top+=d;
  GrowCard(d);
  LayoutStatus();
 }
 // Die feste Fenstergröße (MinimumSize = MaximumSize) vorher lösen und danach neu setzen.
 void GrowCard(int d) {
  if(d==0||updateCard==null)return;
  bool locked=MinimumSize==MaximumSize&&MinimumSize!=Size.Empty;
  MinimumSize=Size.Empty;MaximumSize=Size.Empty;
  int bottom=updateCard.Bottom;
  foreach(Control c in Controls)if(c!=updateCard&&c.Top>=bottom)c.Top+=d;
  updateCard.Height+=d;
  ClientSize=new Size(ClientSize.Width,ClientSize.Height+d);
  if(locked){MinimumSize=Size;MaximumSize=Size;}
 }
 void ShowStatus(UpdateStatus st) {
  string glyph;Color color=Ui.Muted,textColor=Ui.TextColor;
  switch(st.State) {
   case UpdateState.Current:glyph="";color=Ui.SuccessColor;break;
   case UpdateState.Available:glyph="";color=Ui.Accent;textColor=Ui.Accent;break;
   case UpdateState.Error:glyph="";color=Ui.ErrorColor;break;
   case UpdateState.Checking:glyph="";textColor=Ui.Muted;break;
   default:glyph="";break;
  }
  statusGlyph.Glyph=glyph;statusGlyph.GlyphColor=color;
  statusText.Text=st.Text;statusText.ForeColor=textColor;
  updateTips.SetToolTip(statusText,st.Text);
  statusDetail.Text=st.Detail??"";
  updateTips.SetToolTip(statusDetail,st.Detail??"");
  moreValue=st.More??"";
  moreLink.Visible=moreValue.Length>0;
  if(st.State==UpdateState.Available&&hooks.CanInstall!=null&&hooks.CanInstall()) {
   mode=ButtonMode.Install;statusButton.Text="Aktualisieren";statusButton.Kind=ButtonKind.Primary;statusButton.Glyph="";
  } else if(st.State==UpdateState.Available) {
   mode=ButtonMode.Page;statusButton.Text="Release-Seite";statusButton.Kind=ButtonKind.Secondary;statusButton.Glyph="";
  } else {
   mode=ButtonMode.Check;statusButton.Text="Jetzt prüfen";statusButton.Kind=ButtonKind.Secondary;statusButton.Glyph="";
  }
  statusButton.Enabled=st.State!=UpdateState.Checking;
  // Neuer Stand: Feld schließen (auch wenn er keinen Detailtext hat).
  SetDetailsOpen(false);
 }
 async Task StatusButtonClick() {
  if(mode==ButtonMode.Install){Close();if(hooks.Install!=null)hooks.Install();return;}
  if(mode==ButtonMode.Page){if(hooks.OpenPage!=null)hooks.OpenPage();return;}
  ShowStatus(new UpdateStatus{State=UpdateState.Checking,Text="Prüfe auf GitHub …",Detail=""});
  UpdateStatus result;
  try {result=await hooks.CheckNow();}
  catch(Exception ex){result=UpdateStatus.FromError(ex);}
  if(!IsDisposed)ShowStatus(result);
 }
}

// Stand der Update-Prüfung für das Info-Fenster.
public enum UpdateState {Unknown,Current,Available,Error,Checking}
public class UpdateStatus {
 public UpdateState State;
 public string Text,Detail;
 // Technischer Originaltext für "Details" (leer = kein "Details").
 public string More="";
 // Fehler der Prüfung als Stand: feste Kurzmeldung (Message) mit Originaltext in Data["Detail"];
 // unbekannte Fehler nur mit "Details". "Nur Vorabversionen" ist kein Fehler.
 public static UpdateStatus FromError(Exception ex) {
  if(ex.Data.Contains("Prerelease"))return new UpdateStatus{State=UpdateState.Unknown,Text="Nur Vorabversionen",Detail="Noch keine fertige Version."};
  if(ex.Data.Contains("Detail"))return new UpdateStatus{State=UpdateState.Error,Text="Prüfung nicht möglich",Detail=ex.Message,More=ex.Data["Detail"] as string??""};
  return new UpdateStatus{State=UpdateState.Error,Text="Prüfung nicht möglich",Detail="",More=ex.Message};
 }
}
// Verbindung des Info-Fensters zur Update-Prüfung im Hauptfenster (Globale Richtlinien, Abschnitt "Updates").
public class UpdateHooks {
 public Func<UpdateStatus> Status;
 public Func<bool> Auto;
 public Action<bool> SetAuto;
 public Func<Task<UpdateStatus>> CheckNow;
 public Func<bool> CanInstall;
 public Action Install;
 public Action OpenPage;
 // "Automatisch aktualisieren" (ohne diese Hooks fehlt die Zeile im Info-Fenster).
 public Func<bool> AutoInstall;
 public Action<bool> SetAutoInstall;
}

// Seite "Programm hinzufügen" (im Hauptfenster eingebettet, Überschrift kommt von dort):
// listet geöffnete Fenster; gespeichert wird nur die Fensterposition.
public class AddProgramForm:FixedSizeForm {
 class WindowChoice {
  public string Title,Process,Exe,Description;
  // Programm hat mehrere Fenster in der Liste (z.B. Steam: Hauptfenster und Freunde).
  public bool Shared;
  // Programm steht schon ohne Fenstertitel in der Liste: weitere Fenster nur mit Fenstertitel.
  public bool Listed;
 }
 // Alle gefundenen Fenster; die Liste zeigt die, die zur Suche passen.
 List<WindowChoice> allChoices=new List<WindowChoice>();
 readonly TextBox searchBox=new TextBox();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,string lParam);
 readonly List<Entry> existingEntries=new List<Entry>();
 readonly CheckCell onlyThis=new CheckCell();
 readonly Label onlyThisLabel=new Label();
 readonly ListBox windows=new ListBox();
 readonly TextBox nameBox=new TextBox();
 readonly ModernButton addButton;
 readonly HashSet<string> excluded;
 readonly HashSet<string> usedIds;
 public Entry Result;
 public AddProgramForm(Icon icon,IEnumerable<Entry> existing) {
  excluded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  usedIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in existing) {
   existingEntries.Add(entry);
   // Programme mit Fenstertitel schließen nur dieses Fenster aus, nicht das ganze Programm.
   if(!String.IsNullOrEmpty(entry.WindowTitle))continue;
   if(!String.IsNullOrEmpty(entry.ProcessName))excluded.Add(entry.ProcessName);
   if(!String.IsNullOrEmpty(entry.AltProcessName))excluded.Add(entry.AltProcessName);
   usedIds.Add(entry.Id);
  }
  Text="Programm hinzufügen";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(640),pad=Ui.Px(24),y=Ui.Px(4),inner=w-2*pad;
  var intro=new Label{Text="Wähle ein geöffnetes Fenster. Gespeichert wird nur seine Position und Größe – das Programm wird dafür nicht beendet.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(40))};
  Controls.Add(intro);
  y+=Ui.Px(46);
  // Suche (ab 2.52): filtert nach Fenstertitel, Programm (.exe) und Beschreibung.
  var searchCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(36)),Padding=new Padding(Ui.Px(10),Ui.Px(8),Ui.Px(10),0)};
  searchBox.Dock=DockStyle.Fill;searchBox.BorderStyle=BorderStyle.None;searchBox.BackColor=Ui.Surface;searchBox.ForeColor=Ui.TextColor;searchBox.Font=Ui.UiFont;
  searchBox.HandleCreated+=(s,e)=>SendMessage(searchBox.Handle,0x1501,new IntPtr(1),"Suchen – Fenstertitel oder Programm, z.B. Steam");
  searchBox.TextChanged+=(s,e)=>ApplyFilter();
  searchBox.KeyDown+=(s,e)=>{
   // Pfeiltasten wählen in der Liste, Enter fügt hinzu.
   if(e.KeyCode==Keys.Down&&windows.Items.Count>0){windows.SelectedIndex=Math.Min(windows.Items.Count-1,windows.SelectedIndex+1);e.Handled=true;}
   else if(e.KeyCode==Keys.Up&&windows.Items.Count>0){windows.SelectedIndex=Math.Max(0,windows.SelectedIndex-1);e.Handled=true;}
   else if(e.KeyCode==Keys.Enter&&addButton.Enabled){Accept();e.Handled=e.SuppressKeyPress=true;}
  };
  searchCard.Controls.Add(searchBox);
  Controls.Add(searchCard);
  y+=searchCard.Height+Ui.Px(10);
  var listCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(268)),Padding=new Padding(Ui.Px(4))};
  windows.Dock=DockStyle.Fill;windows.BorderStyle=BorderStyle.None;windows.IntegralHeight=false;
  windows.DrawMode=DrawMode.OwnerDrawFixed;windows.ItemHeight=Ui.Px(44);
  windows.BackColor=Ui.Surface;windows.ForeColor=Ui.TextColor;
  windows.DrawItem+=DrawWindow;
  windows.HandleCreated+=(s,e)=>Native.DarkScroll(windows);
  windows.SelectedIndexChanged+=(s,e)=>{
   var choice=windows.SelectedItem as WindowChoice;
   if(choice!=null) {
    // Mehrere Fenster eines Programms: Fenstertitel als Name vorschlagen und nur dieses Fenster aufnehmen.
    nameBox.Text=choice.Shared||choice.Listed?choice.Title:String.IsNullOrEmpty(choice.Description)?choice.Process:choice.Description;
    onlyThis.Checked=choice.Shared||choice.Listed;
    // Steht das Programm schon ohne Fenstertitel in der Liste, geht nur "dieses Fenster".
    onlyThis.Enabled=!choice.Listed;
    onlyThisLabel.Text="Nur das Fenster „"+choice.Title+"“ (für Programme mit mehreren Fenstern, z.B. Steam und Freundesliste)";
   }
   UpdateButtons();
  };
  windows.DoubleClick+=(s,e)=>{if(windows.SelectedItem!=null)Accept();};
  listCard.Controls.Add(windows);
  Controls.Add(listCard);
  y+=listCard.Height+Ui.Px(14);
  var nameLabel=new Label{Text="Name in der Liste",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(22))};
  Controls.Add(nameLabel);
  y+=Ui.Px(24);
  var nameCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(36)),Padding=new Padding(Ui.Px(10),Ui.Px(8),Ui.Px(10),0)};
  nameBox.Dock=DockStyle.Fill;nameBox.BorderStyle=BorderStyle.None;nameBox.BackColor=Ui.Surface;nameBox.ForeColor=Ui.TextColor;nameBox.Font=Ui.UiFont;
  nameBox.TextChanged+=(s,e)=>UpdateButtons();
  nameCard.Controls.Add(nameBox);
  Controls.Add(nameCard);
  y+=nameCard.Height+Ui.Px(10);
  // Fenstertitel (ab 2.52): Eintrag gilt nur für dieses Fenster des Programms.
  onlyThis.BackColor=Ui.Bg;onlyThis.Bounds=new Rectangle(pad-Ui.Px(6),y,Ui.Px(30),Ui.Px(28));
  onlyThisLabel.Font=Ui.SmallFont;onlyThisLabel.ForeColor=Ui.TextColor;onlyThisLabel.BackColor=Ui.Bg;onlyThisLabel.AutoEllipsis=true;onlyThisLabel.UseMnemonic=false;
  onlyThisLabel.TextAlign=ContentAlignment.MiddleLeft;onlyThisLabel.Cursor=Cursors.Hand;
  onlyThisLabel.Bounds=new Rectangle(onlyThis.Right+Ui.Px(4),y,inner-Ui.Px(28),Ui.Px(28));
  onlyThisLabel.Click+=(s,e)=>{if(onlyThis.Enabled)onlyThis.Checked=!onlyThis.Checked;};
  Controls.Add(onlyThis);Controls.Add(onlyThisLabel);
  y+=Ui.Px(28)+Ui.Px(14);
  var refresh=new ModernButton("Aktualisieren",ButtonKind.Secondary,"");
  refresh.Location=new Point(pad,y);
  refresh.Click+=(s,e)=>LoadWindows();
  Controls.Add(refresh);
  addButton=new ModernButton("Hinzufügen",ButtonKind.Primary,null);
  var cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  // Wie bei den Einstellungsseiten: "Abbrechen" links, Primärknopf ganz rechts, 10 Abstand.
  addButton.Location=new Point(w-pad-addButton.Width,y);
  cancel.Location=new Point(addButton.Left-Ui.Px(10)-cancel.Width,y);
  addButton.Click+=(s,e)=>Accept();
  Controls.Add(cancel);Controls.Add(addButton);
  CancelButton=cancel;
  y+=Ui.Px(38)+Ui.Px(20);
  ClientSize=new Size(w,y);
  // Im Hauptfenster füllt die Seite den Inhaltsbereich: Liste wächst mit, Name und Knöpfe bleiben unten.
  // (Anker erst nach der endgültigen Startgröße setzen, sonst stimmen die Abstände nicht.)
  intro.Anchor=searchCard.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  listCard.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  nameLabel.Anchor=nameCard.Anchor=onlyThisLabel.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  onlyThis.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  refresh.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  addButton.Anchor=cancel.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
  LoadWindows();
  ActiveControl=searchBox;
 }
 void UpdateButtons(){addButton.Enabled=windows.SelectedItem!=null&&nameBox.Text.Trim().Length>0;}
 void LoadWindows() {
  var choices=new List<WindowChoice>();
  if(MainForm.DemoMode) {
   // Screenshots: feste Demo-Liste statt der tatsächlich offenen Fenster (deren Titel privat sein können).
   choices.Add(new WindowChoice{Title="Downloads – Datei-Explorer",Process="explorer",Exe=@"C:\Windows\explorer.exe",Description="Windows-Explorer"});
   choices.Add(new WindowChoice{Title="Paint",Process="mspaint",Exe=@"C:\Windows\System32\mspaint.exe",Description="Paint"});
   choices.Add(new WindowChoice{Title="Rechner",Process="CalculatorApp",Exe=@"C:\Program Files\WindowsApps\CalculatorApp.exe",Description="Rechner"});
   choices.Add(new WindowChoice{Title="Terminal",Process="WindowsTerminal",Exe=@"C:\Program Files\WindowsApps\WindowsTerminal.exe",Description="Terminal"});
   choices.Add(new WindowChoice{Title="Unbenannt – Editor",Process="Notepad",Exe=@"C:\Windows\System32\notepad.exe",Description="Editor"});
   allChoices=choices;
   ApplyFilter();
   windows.SelectedIndex=choices.Count-1;
   UpdateButtons();
   return;
  }
  // Alle sichtbaren Fenster mit Titel (nicht nur das Hauptfenster je Programm), damit z.B. bei Steam das
  // Hauptfenster und die Freundesliste getrennt wählbar sind.
  int own=Process.GetCurrentProcess().Id;
  var infos=new Dictionary<int,WindowChoice>();
  foreach(var window in VisibleAlignment.TitledWindows()) {
   if(window.ProcessId==own)continue;
   WindowChoice info;
   if(!infos.TryGetValue(window.ProcessId,out info)) {
    info=null;
    try {
     using(var process=Process.GetProcessById(window.ProcessId)) {
      info=new WindowChoice{Process=process.ProcessName};
      // Pfad und Beschreibung sind bei Programmen mit Administratorrechten ohne eigene Adminrechte nicht lesbar.
      try {
       info.Exe=process.MainModule.FileName;
       info.Description=FileVersionInfo.GetVersionInfo(info.Exe).FileDescription;
       if(info.Description!=null) {
        info.Description=info.Description.Trim();
        // Manche Programme (z.B. der Windows-11-Editor) tragen nur den Dateinamen ein: "Notepad.exe" -> "Notepad".
        if(info.Description.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))info.Description=info.Description.Substring(0,info.Description.Length-4);
       }
      } catch {}
     }
    } catch {}
    infos[window.ProcessId]=info;
   }
   if(info==null)continue;
   string title=window.Title.Trim();
   // Schon mit genau diesem Fenstertitel in der Liste.
   if(existingEntries.Any(x=>!String.IsNullOrEmpty(x.WindowTitle)&&String.Equals(x.ProcessName,info.Process,StringComparison.OrdinalIgnoreCase)&&String.Equals(x.WindowTitle.Trim(),title,StringComparison.CurrentCultureIgnoreCase)))continue;
   choices.Add(new WindowChoice{Title=title,Process=info.Process,Exe=info.Exe,Description=info.Description,Listed=excluded.Contains(info.Process)});
  }
  foreach(var choice in choices)choice.Shared=choices.Count(x=>String.Equals(x.Process,choice.Process,StringComparison.OrdinalIgnoreCase))>1;
  choices.Sort((a,b)=>String.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase));
  allChoices=choices;
  ApplyFilter();
 }
 // Liste nach dem Suchtext filtern (alle Wörter müssen in Titel, Programm oder Beschreibung vorkommen).
 void ApplyFilter() {
  var previous=windows.SelectedItem as WindowChoice;
  string[] words=searchBox.Text.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
  var shown=allChoices.Where(c=>words.All(word=>(c.Title+" "+c.Process+".exe "+(c.Description??"")).IndexOf(word,StringComparison.CurrentCultureIgnoreCase)>=0)).ToList();
  windows.BeginUpdate();
  windows.Items.Clear();
  foreach(var c in shown)windows.Items.Add(c);
  windows.EndUpdate();
  if(previous!=null&&shown.Contains(previous))windows.SelectedItem=previous;
  else if(windows.Items.Count>0)windows.SelectedIndex=0;
  else nameBox.Text="";
  UpdateButtons();
 }
 void DrawWindow(object sender,DrawItemEventArgs e) {
  var g=e.Graphics;
  using(var back=new SolidBrush(Ui.Surface)) g.FillRectangle(back,e.Bounds);
  if(e.Index<0||e.Index>=windows.Items.Count)return;
  var choice=(WindowChoice)windows.Items[e.Index];
  bool selected=(e.State&DrawItemState.Selected)==DrawItemState.Selected;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=Rectangle.Inflate(e.Bounds,-Ui.Px(2),-Ui.Px(2));
  if(selected) {
   using(var path=Ui.Round(r,Ui.Px(4)))
   using(var brush=new SolidBrush(Ui.Blend(Ui.Surface,Ui.Accent,0.16f))) g.FillPath(brush,path);
   // Schmale Akzentmarke links wie in Windows-11-Listen.
   using(var path=Ui.Round(new Rectangle(r.X+Ui.Px(2),r.Y+r.Height/2-Ui.Px(8),Ui.Px(3),Ui.Px(16)),Ui.Px(1)))
   using(var brush=new SolidBrush(Ui.Accent)) g.FillPath(brush,path);
  }
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPadding;
  int x=r.X+Ui.Px(14),width=r.Width-Ui.Px(24);
  TextRenderer.DrawText(g,choice.Title,Ui.UiSemibold,new Rectangle(x,r.Y+Ui.Px(4),width,Ui.Px(20)),Ui.TextColor,flags);
  string detail=choice.Process+".exe"+(String.IsNullOrEmpty(choice.Description)?"":" · "+choice.Description)+(choice.Listed?" · schon in der Liste – nur mit Fenstertitel":choice.Shared?" · mehrere Fenster":"")+(choice.Exe==null?" · Pfad nicht lesbar (Administrator?)":"");
  TextRenderer.DrawText(g,detail,Ui.SmallFont,new Rectangle(x,r.Y+Ui.Px(22),width,Ui.Px(18)),Ui.Muted,flags);
 }
 void Accept() {
  var choice=windows.SelectedItem as WindowChoice;
  string name=nameBox.Text.Trim();
  if(choice==null||name.Length==0)return;
  string windowTitle=onlyThis.Checked||choice.Listed?choice.Title:null;
  var id=new StringBuilder("eigen-");
  foreach(char c in (choice.Process+(windowTitle!=null?"-"+windowTitle:"")).ToLowerInvariant())id.Append(Char.IsLetterOrDigit(c)?c:'-');
  string baseId=id.ToString(),unique=baseId;
  for(int i=2;usedIds.Contains(unique);i++)unique=baseId+"-"+i;
  Result=new Entry{Id=unique,Name=name,ProcessName=choice.Process,Exe=choice.Exe,WindowOnly=true,Keys=new[]{"Window"},WindowTitle=windowTitle};
  DialogResult=DialogResult.OK;
  Close();
 }
}

// Seite "Einmalig ausrichten" (im Hauptfenster eingebettet): ein beliebiges offenes Fenster anklicken bzw. in der Liste wählen und ausrichten,
// ohne es in die Liste aufzunehmen. Es wird nichts gespeichert (keine Regel, keine Nachkorrektur).
public class OneTimeAlignForm:FixedSizeForm {
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder name,int max);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
 // Desktop, Taskleiste, Startmenü und Infobereich zählen nicht als Fenster.
 static readonly string[] IgnoredClasses={"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd","Windows.UI.Core.CoreWindow","NotifyIconOverflowWindow","TopLevelWindowForOverflowXamlIsland","XamlExplorerHostIslandWindow"};
 // Offenes Fenster in der Liste.
 class WindowItem {public IntPtr Handle;public string Title,Process;}
 // Ergebnis einer Ausrichtung (bleibt bis zum Programmende erhalten, neueste oben).
 class Result {public DateTime Time;public string Title,Text;public bool Failed;}
 static readonly List<Result> recent=new List<Result>();
 readonly Func<int> gap;
 readonly Action<string> log;
 readonly Label intro,windowTitle,windowDetail,status,listTitle,recentTitle,recentEmpty;
 readonly Card card,listCard,recentCard;
 readonly ModernButton pick,refresh;
 readonly ListBox windows=new ListBox(),results=new ListBox();
 readonly List<ModernButton> directions=new List<ModernButton>();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=150};
 readonly uint ownProcess=(uint)Process.GetCurrentProcess().Id;
 IntPtr target=IntPtr.Zero;
 string targetName="";
 bool picking,working,loading,demo;
 public OneTimeAlignForm(Icon icon,Func<int> gap,Action<string> log) {
  this.gap=gap;this.log=log;
  Text="Einmalig ausrichten";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  KeyPreview=true;
  int w=Ui.Px(760);
  intro=new Label{Text="Ein beliebiges offenes Fenster anklicken oder unten in der Liste wählen, dann die Richtung anklicken. Es wird nicht in die Liste der Programme aufgenommen und nichts gespeichert.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  Controls.Add(intro);
  card=new Card{Size=new Size(Ui.Px(600),Ui.Px(64))};
  pick=new ModernButton("Fenster anklicken",ButtonKind.Primary,"");
  pick.Click+=(s,e)=>StartPick();
  windowTitle=new Label{Text="Noch kein Fenster gewählt",Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false};
  windowDetail=new Label{Text="„Fenster anklicken“ und danach auf das gewünschte Fenster klicken – oder unten in der Liste wählen",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",AutoEllipsis=true,UseMnemonic=false};
  card.Controls.Add(windowTitle);card.Controls.Add(windowDetail);card.Controls.Add(pick);
  card.Resize+=(s,e)=>LayoutCard();
  Controls.Add(card);
  string[] modes={"top","bottom","left","right","hcenter","vcenter"};
  string[] names={"Oben","Unten","Links","Rechts","↔ Mitte","↕ Mitte"};
  string[] glyphs={"","","","",null,null};
  for(int i=0;i<modes.Length;i++) {
   var button=new ModernButton(names[i],ButtonKind.Secondary,glyphs[i]);
   string mode=modes[i],name=names[i];
   button.Click+=async(s,e)=>await Apply(mode,name);
   directions.Add(button);Controls.Add(button);
  }
  status=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",AutoEllipsis=true,UseMnemonic=false};
  Controls.Add(status);
  // Links: offene Fenster zum Auswählen (falls das Fenster verdeckt ist), rechts: zuletzt ausgerichtet.
  listTitle=new Label{Text="Offene Fenster",Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Bg,AutoSize=true};
  Controls.Add(listTitle);
  refresh=new ModernButton("Aktualisieren",ButtonKind.Ghost,"");
  refresh.Height=Ui.Px(32);refresh.Margin=new Padding(0);
  refresh.Click+=(s,e)=>LoadWindows();
  Controls.Add(refresh);
  listCard=new Card{Padding=new Padding(Ui.Px(4))};
  SetupList(windows,Ui.Px(44),DrawWindow);
  windows.SelectedIndexChanged+=(s,e)=>ChooseFromList();
  listCard.Controls.Add(windows);
  Controls.Add(listCard);
  recentTitle=new Label{Text="Zuletzt ausgerichtet",Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Bg,AutoSize=true};
  Controls.Add(recentTitle);
  recentCard=new Card{Padding=new Padding(Ui.Px(4))};
  SetupList(results,Ui.Px(44),DrawResult);
  results.SelectionMode=SelectionMode.None;
  recentEmpty=new Label{Text="Noch nichts ausgerichtet.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Fill};
  recentCard.Controls.Add(results);recentCard.Controls.Add(recentEmpty);
  Controls.Add(recentCard);
  ClientSize=new Size(w,Ui.Px(560));
  Resize+=(s,e)=>Arrange();
  HandleCreated+=(s,e)=>{Arrange();PerformLayout();};
  Arrange();
  timer.Tick+=(s,e)=>CheckForeground();
  FormClosed+=(s,e)=>{timer.Stop();timer.Dispose();};
  LoadWindows();
  ShowResults();
  UpdateButtons();
 }
 void SetupList(ListBox box,int itemHeight,DrawItemEventHandler draw) {
  box.Dock=DockStyle.Fill;box.BorderStyle=BorderStyle.None;box.IntegralHeight=false;
  box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=itemHeight;
  box.BackColor=Ui.Surface;box.ForeColor=Ui.TextColor;
  box.DrawItem+=draw;
  box.HandleCreated+=(s,e)=>Native.DarkScroll((Control)s);
 }
 void LayoutCard() {
  pick.Location=new Point(card.Width-Ui.Px(13)-pick.Width,(card.Height-pick.Height)/2);
  int textWidth=Math.Max(Ui.Px(40),pick.Left-Ui.Px(28));
  windowTitle.Bounds=new Rectangle(Ui.Px(16),Ui.Px(10),textWidth,Ui.Px(22));
  windowDetail.Bounds=new Rectangle(Ui.Px(16),Ui.Px(33),textWidth,Ui.Px(20));
 }
 // Seite füllt den Inhaltsbereich: oben Auswahl und Richtungen, darunter zwei Listen nebeneinander.
 // Selbst angeordnete Bildlauf-Seite (Globale Richtlinien): nicht anordnen, solange die Seite noch nicht im Fenster
 // sitzt oder das Fenster minimiert ist – sonst bleibt ein zu großer Bildlaufbereich mit leerer Fläche.
 bool ReadyToArrange() {
  if(!IsHandleCreated||ClientSize.Width<Ui.Px(400))return false;
  var top=TopLevelControl as Form;
  return top==null||top.WindowState!=FormWindowState.Minimized;
 }
 void Arrange() {
  if(!ReadyToArrange())return;
  int pad=Ui.Px(24),inner=Math.Max(Ui.Px(500),ClientSize.Width-2*pad),y=Ui.Px(4);
  int h=TextRenderer.MeasureText(intro.Text,intro.Font,new Size(inner,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+Ui.Px(4);
  intro.Bounds=new Rectangle(pad,y,inner,h);
  y+=h+Ui.Px(14);
  card.Bounds=new Rectangle(pad,y,inner,Ui.Px(64));
  y+=card.Height+Ui.Px(16);
  int x=pad;
  foreach(var button in directions){button.Location=new Point(x,y);x+=button.Width+Ui.Px(6);}
  y+=Ui.Px(38)+Ui.Px(10);
  status.Bounds=new Rectangle(pad,y,inner,Ui.Px(40));
  y+=Ui.Px(40)+Ui.Px(12);
  int gapBetween=Ui.Px(16);
  int left=(int)((inner-gapBetween)*0.58),right=inner-gapBetween-left;
  int headH=Ui.Px(32);
  listTitle.Location=new Point(pad,y+(headH-listTitle.Height)/2);
  refresh.Location=new Point(pad+left-refresh.Width,y+(headH-refresh.Height)/2);
  recentTitle.Location=new Point(pad+left+gapBetween,y+(headH-recentTitle.Height)/2);
  y+=headH+Ui.Px(6);
  int listH=Math.Max(Ui.Px(140),ClientSize.Height-Ui.Px(20)-y);
  listCard.Bounds=new Rectangle(pad,y,left,listH);
  recentCard.Bounds=new Rectangle(pad+left+gapBetween,y,right,listH);
 }
 // Vom Hauptfenster nach dem Anzeigen aufgerufen: gleich auf den Klick warten, meist ist genau das gewünscht.
 public void Begin(){StartPick();}
 protected override void OnKeyDown(KeyEventArgs e) {
  base.OnKeyDown(e);
  if(e.KeyCode!=Keys.Escape)return;
  e.Handled=true;
  if(picking){StopPick();status.Text="Auswahl abgebrochen.";}
  else Close();
 }
 void UpdateButtons() {
  bool ready=(target!=IntPtr.Zero||demo)&&!picking&&!working;
  foreach(var button in directions)button.Enabled=ready;
  pick.Enabled=!picking&&!working;
  windows.Enabled=!working;
 }
 void StartPick() {
  if(working)return;
  picking=true;
  status.Text="Jetzt auf das gewünschte Fenster klicken … (Esc = abbrechen)";
  timer.Start();
  UpdateButtons();
 }
 void StopPick(){picking=false;timer.Stop();UpdateButtons();}
 // Das angeklickte Fenster wird zum Vordergrundfenster; eigene Fenster, Desktop und Taskleiste werden übergangen.
 void CheckForeground() {
  IntPtr handle=GetForegroundWindow();
  if(handle==IntPtr.Zero)return;
  IntPtr root=GetAncestor(handle,2);
  if(root!=IntPtr.Zero)handle=root;
  uint process;
  GetWindowThreadProcessId(handle,out process);
  if(process==ownProcess||!IsWindowVisible(handle))return;
  var cls=new StringBuilder(256);
  GetClassName(handle,cls,cls.Capacity);
  if(IgnoredClasses.Contains(cls.ToString()))return;
  var title=new StringBuilder(512);
  GetWindowText(handle,title,title.Capacity);
  string processName="";
  try {using(var p=Process.GetProcessById((int)process))processName=p.ProcessName+".exe";} catch {}
  SetTarget(handle,title.ToString(),processName);
  // Das angeklickte Fenster liegt jetzt vorn: Programmfenster wieder nach vorn holen, damit die Richtungen erreichbar sind.
  var top=TopLevelControl as Form;
  if(top!=null)top.Activate();
 }
 void SetTarget(IntPtr handle,string title,string processName) {
  target=handle;
  targetName=title.Length>0?title:(processName.Length>0?processName:"Fenster");
  windowTitle.Text=targetName;
  windowDetail.Text=processName.Length>0?processName:"Prozess nicht lesbar";
  StopPick();
  status.Text="Fenster gewählt – jetzt die Richtung anklicken. Mehrere Richtungen nacheinander ergänzen sich.";
  // Auswahl in der Liste mitführen (ohne erneut auszuwählen).
  loading=true;
  int index=-1;
  for(int i=0;i<windows.Items.Count;i++)if(((WindowItem)windows.Items[i]).Handle==handle){index=i;break;}
  windows.SelectedIndex=index;
  loading=false;
 }
 void ChooseFromList() {
  if(loading)return;
  var item=windows.SelectedItem as WindowItem;
  if(item==null||item.Handle==IntPtr.Zero)return;
  if(!IsWindow(item.Handle)){status.Text="Das Fenster ist nicht mehr geöffnet – „Aktualisieren“ lädt die Liste neu.";return;}
  SetTarget(item.Handle,item.Title,item.Process);
 }
 void LoadWindows() {
  var items=new List<WindowItem>();
  if(MainForm.DemoMode) {
   // Screenshots: feste Demo-Liste statt der tatsächlich offenen Fenster (deren Titel privat sein können).
   items.Add(new WindowItem{Title="Downloads – Datei-Explorer",Process="explorer.exe"});
   items.Add(new WindowItem{Title="Paint",Process="mspaint.exe"});
   items.Add(new WindowItem{Title="Rechner",Process="CalculatorApp.exe"});
   items.Add(new WindowItem{Title="Unbenannt – Editor",Process="Notepad.exe"});
  } else {
   // Alle sichtbaren Fenster mit Titel (ab 2.52 auch weitere Fenster eines Programms, z.B. die Steam-Freundesliste).
   var names=new Dictionary<int,string>();
   foreach(var window in VisibleAlignment.TitledWindows()) {
    try {
     if(window.ProcessId==(int)ownProcess)continue;
     var cls=new StringBuilder(256);
     GetClassName(window.Handle,cls,cls.Capacity);
     if(IgnoredClasses.Contains(cls.ToString()))continue;
     string name;
     if(!names.TryGetValue(window.ProcessId,out name)) {
      try {using(var process=Process.GetProcessById(window.ProcessId))name=process.ProcessName+".exe";}
      catch {name="";}
      names[window.ProcessId]=name;
     }
     if(name.Length==0)continue;
     items.Add(new WindowItem{Handle=window.Handle,Title=window.Title.Trim(),Process=name});
    } catch {}
   }
   items.Sort((a,b)=>String.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase));
  }
  loading=true;
  windows.BeginUpdate();
  windows.Items.Clear();
  foreach(var item in items)windows.Items.Add(item);
  windows.EndUpdate();
  windows.SelectedIndex=-1;
  for(int i=0;i<windows.Items.Count;i++)if(target!=IntPtr.Zero&&((WindowItem)windows.Items[i]).Handle==target)windows.SelectedIndex=i;
  loading=false;
 }
 void ShowResults() {
  results.BeginUpdate();
  results.Items.Clear();
  foreach(var r in recent)results.Items.Add(r);
  results.EndUpdate();
  recentEmpty.Visible=recent.Count==0;
  results.Visible=recent.Count>0;
 }
 void AddResult(string text,bool failed) {
  recent.Insert(0,new Result{Time=DateTime.Now,Title=targetName,Text=text,Failed=failed});
  if(recent.Count>30)recent.RemoveAt(recent.Count-1);
  ShowResults();
 }
 // Zwei Zeilen wie in "Programm hinzufügen": Titel fett, darunter grau der Prozess bzw. das Ergebnis.
 void DrawRow(DrawItemEventArgs e,ListBox box,string title,string detail,string right,Color detailColor) {
  var g=e.Graphics;
  using(var back=new SolidBrush(Ui.Surface)) g.FillRectangle(back,e.Bounds);
  bool selected=box.SelectionMode!=SelectionMode.None&&(e.State&DrawItemState.Selected)==DrawItemState.Selected;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=Rectangle.Inflate(e.Bounds,-Ui.Px(2),-Ui.Px(2));
  if(selected) {
   using(var path=Ui.Round(r,Ui.Px(4)))
   using(var brush=new SolidBrush(Ui.Blend(Ui.Surface,Ui.Accent,0.16f))) g.FillPath(brush,path);
   using(var path=Ui.Round(new Rectangle(r.X+Ui.Px(2),r.Y+r.Height/2-Ui.Px(8),Ui.Px(3),Ui.Px(16)),Ui.Px(1)))
   using(var brush=new SolidBrush(Ui.Accent)) g.FillPath(brush,path);
  }
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPadding;
  int x=r.X+Ui.Px(14),width=r.Width-Ui.Px(24);
  int rightWidth=0;
  if(right!=null) {
   rightWidth=TextRenderer.MeasureText(right,Ui.SmallFont,Size.Empty,flags).Width+Ui.Px(8);
   TextRenderer.DrawText(g,right,Ui.SmallFont,new Rectangle(r.Right-Ui.Px(10)-rightWidth,r.Y+Ui.Px(5),rightWidth,Ui.Px(20)),Ui.Muted,flags|TextFormatFlags.Right);
  }
  TextRenderer.DrawText(g,title,Ui.UiSemibold,new Rectangle(x,r.Y+Ui.Px(4),width-rightWidth,Ui.Px(20)),Ui.TextColor,flags);
  TextRenderer.DrawText(g,detail,Ui.SmallFont,new Rectangle(x,r.Y+Ui.Px(22),width,Ui.Px(18)),detailColor,flags);
 }
 void DrawWindow(object sender,DrawItemEventArgs e) {
  if(e.Index<0||e.Index>=windows.Items.Count){using(var back=new SolidBrush(Ui.Surface))e.Graphics.FillRectangle(back,e.Bounds);return;}
  var item=(WindowItem)windows.Items[e.Index];
  DrawRow(e,windows,item.Title,item.Process,null,Ui.Muted);
 }
 void DrawResult(object sender,DrawItemEventArgs e) {
  if(e.Index<0||e.Index>=results.Items.Count){using(var back=new SolidBrush(Ui.Surface))e.Graphics.FillRectangle(back,e.Bounds);return;}
  var item=(Result)results.Items[e.Index];
  DrawRow(e,results,item.Title,item.Text,item.Time.ToString("HH:mm"),item.Failed?Ui.ErrorColor:Ui.Muted);
 }
 // Screenshots: Beispielwerte statt echter Fenster (nichts wird verschoben).
 internal void DemoFill() {
  demo=true;
  targetName="Unbenannt – Editor";
  windowTitle.Text=targetName;windowDetail.Text="Notepad.exe";
  windows.SelectedIndex=windows.Items.Count-1;
  recent.Clear();
  recent.Add(new Result{Time=DateTime.Today.AddHours(22).AddMinutes(4),Title="Unbenannt – Editor",Text="Rechts: 18 px"});
  recent.Add(new Result{Time=DateTime.Today.AddHours(22).AddMinutes(3),Title="Unbenannt – Editor",Text="Unten: 18 px"});
  recent.Add(new Result{Time=DateTime.Today.AddHours(21).AddMinutes(58),Title="Rechner",Text="Waagerecht mittig"});
  status.Text="Fenster gewählt – jetzt die Richtung anklicken. Mehrere Richtungen nacheinander ergänzen sich.";
  ShowResults();
  UpdateButtons();
 }
 async Task Apply(string mode,string name) {
  if(demo||target==IntPtr.Zero||working||picking)return;
  if(!IsWindow(target)) {
   target=IntPtr.Zero;windowTitle.Text="Noch kein Fenster gewählt";windowDetail.Text="Das Fenster ist nicht mehr geöffnet";
   status.Text="Bitte erneut „Fenster anklicken“ oder ein Fenster in der Liste wählen.";UpdateButtons();LoadWindows();return;
  }
  working=true;UpdateButtons();
  IntPtr handle=target;int pixels=gap();
  status.Text=name+" …";
  try {
   string result=await Task.Run(()=>VisibleAlignment.AlignHandle(handle,pixels,mode));
   status.Text=result;
   AddResult(result,false);
   log("Einmalig ausrichten – "+targetName+": "+result);
  } catch(Exception ex) {
   status.Text=ex.Message;
   AddResult(ex.Message,true);
   log("Einmalig ausrichten – "+targetName+": "+ex.Message);
  } finally {working=false;UpdateButtons();}
 }
}

// Einstellungsseite "Kanten je Programm" (im Hauptfenster eingebettet): je Programm die Kanten festlegen,
// an denen ausgerichtet wird. Übernommen wird erst mit "Speichern"; "Abbrechen" oder ein Wechsel über die Navigation verwirft.
public class AlignRulesForm:FixedSizeForm {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,string lParam);
 static readonly string[] Directions={"top","bottom","left","right","hcenter","vcenter"};
 static readonly string[] Titles={"Oben","Unten","Links","Rechts","↔ Mitte","↕ Mitte"};
 // Nur Oben/Unten/Links/Rechts haben einen eigenen Abstand; bei Mitte wird gleich verteilt.
 const int EdgeCount=4;
 readonly List<Entry> programs;
 readonly Dictionary<string,CheckCell[]> cells=new Dictionary<string,CheckCell[]>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,TextBox[]> boxes=new Dictionary<string,TextBox[]>(StringComparer.OrdinalIgnoreCase);
 // Nachbarprogramm je Kante (null = Bildschirmrand) und die Beschriftung darunter.
 readonly Dictionary<string,string[]> refs=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Label[]> refLabels=new Dictionary<string,Label[]>(StringComparer.OrdinalIgnoreCase);
 readonly ContextMenuStrip refMenu=new ContextMenuStrip();
 readonly Label status,error;
 // Speichern (Globale Richtlinien, Einstellungsseiten): liefert eine Fehlermeldung oder null; bei Fehler bleibt der Dialog offen.
 public Func<string> Save;
 public Dictionary<string,HashSet<string>> Rules;
 public Dictionary<string,Dictionary<string,int>> Gaps;
 public Dictionary<string,Dictionary<string,string>> Refs;
 // Seitenaufbau: Spalten und Knöpfe passen sich an die Breite an (Arrange), die Tabelle an die Höhe.
 readonly Label description;
 readonly Card card;
 readonly Panel rows,header;
 readonly ModernButton standard,ok,cancel;
 readonly List<Action> placers=new List<Action>();
 readonly int head,rowH;
 readonly ToolTip tips=new ModernToolTip();
 // Zustand beim Öffnen: Vergleich zeigt, ob es ungespeicherte Änderungen gibt (Rückfrage beim Verlassen).
 string initialState;
 // Hinweis auf gegenseitige Nachbarbezüge (steht in der Statuszeile, solange nichts anderes dort steht).
 string cycleNote="";
 int[] colW=new int[6],colLeft=new int[6];
 int colX,measureW,tableWidth;
 // Anzeigeplatz je Programm (Index in programs → Zeile) und Sortierung; die Sortierung gilt bis zum Programmende.
 int[] slot;
 static int sortMode;
 readonly List<Panel> rowPanels=new List<Panel>();
 readonly Label programHead;
 void ApplySort() {
  var order=Enumerable.Range(0,programs.Count).ToList();
  if(sortMode==1)order=order.OrderBy(k=>programs[k].Name,StringComparer.CurrentCultureIgnoreCase).ToList();
  else if(sortMode==2)order=order.OrderByDescending(k=>programs[k].Name,StringComparer.CurrentCultureIgnoreCase).ToList();
  for(int i=0;i<order.Count;i++)slot[order[i]]=i;
  programHead.Text="Programm"+(sortMode==1?"  ↑ A–Z":sortMode==2?"  ↓ Z–A":"");
 }
 public AlignRulesForm(Icon icon,IEnumerable<Entry> list,Dictionary<string,HashSet<string>> current,Dictionary<string,Dictionary<string,int>> currentGaps,Dictionary<string,Dictionary<string,string>> currentRefs,int globalGap) {
  programs=new List<Entry>(list);
  Refs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  refMenu.Opened+=(s,e)=>MenuStyle.RoundCorners(refMenu);
  Rules=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  Gaps=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  Text="Kanten je Programm";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(944),pad=Ui.Px(24),inner=w-2*pad;
  // Ab 2.52 flacher (vorher 64): Zeile 28 hoch, Bezug ("am Rand") direkt darunter.
  head=Ui.Px(32);rowH=Ui.Px(50);
  description=new Label{Text="Angehakte Kanten werden ausgerichtet, nicht angehakte bleiben, wie sie sind. Neben dem Haken steht der Abstand in px: leer = allgemeiner Abstand ("+globalGap+" px), sonst gilt die Zahl nur für dieses Programm. Darunter steht, wovon der Abstand gilt: vom Bildschirmrand oder von einem Nachbarprogramm (anklicken zum Ändern). „Messen“ übernimmt die aktuellen Abstände des offenen Fensters für die angehakten Kanten.\r\n„Automatisch“ richtet alle angehakten Kanten zusammen aus (Oben + Unten = volle Höhe, Links + Rechts = volle Breite). Die Vorgabe steht in der Programmliste; selbst hinzugefügte Programme haben keine.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  Controls.Add(description);
  // Höchstens 10 Zeilen, auf kleinen Bildschirmen weniger (darüber wird in der Tabelle gescrollt).
  int fitRows=(Screen.PrimaryScreen.WorkingArea.Height-Ui.Px(540))/rowH;
  int visibleRows=Math.Max(3,Math.Min(programs.Count,Math.Min(10,fitRows)));
  card=new Card{Bounds=new Rectangle(pad,0,inner,head+visibleRows*rowH+Ui.Px(10)),Padding=new Padding(Ui.Px(1))};
  header=new Panel{Dock=DockStyle.Top,Height=head,BackColor=Ui.Surface};
  // Sortieren (ab 2.52): Klick auf "Programm" wechselt Name A–Z, Name Z–A und die Reihenfolge der Programmliste.
  programHead=new Label{Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=ContentAlignment.MiddleLeft,Cursor=Cursors.Hand,UseMnemonic=false};
  programHead.Click+=(s,e)=>{sortMode=(sortMode+1)%3;ApplySort();Arrange();};
  tips.SetToolTip(programHead,"Klicken zum Sortieren: Name A–Z, Name Z–A, Reihenfolge der Programmliste");
  header.Controls.Add(programHead);
  slot=new int[programs.Count];
  ApplySort();
  placers.Add(()=>programHead.Bounds=new Rectangle(Ui.Px(16),0,colX-Ui.Px(16),head));
  for(int i=0;i<Titles.Length;i++) {
   int index=i;
   var title=new Label{Text=Titles[i],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=i<EdgeCount?ContentAlignment.MiddleLeft:ContentAlignment.MiddleCenter};
   header.Controls.Add(title);
   placers.Add(()=>title.Bounds=index<EdgeCount?new Rectangle(colLeft[index]+Ui.Px(8),0,colW[index]-Ui.Px(8),head):new Rectangle(colLeft[index],0,colW[index],head));
  }
  rows=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Ui.Surface};
  rows.HandleCreated+=(s,e)=>Native.DarkScroll(rows);
  for(int r=0;r<programs.Count;r++) {
   var entry=programs[r];
   // Jede Zeile in einem eigenen Feld, damit sie sich beim Sortieren als Ganzes verschieben lässt.
   var rowPanel=new Panel{BackColor=Ui.Surface};
   rows.Controls.Add(rowPanel);rowPanels.Add(rowPanel);
   int rowIndex=r;
   placers.Add(()=>{rowPanel.Bounds=new Rectangle(0,slot[rowIndex]*rowH,tableWidth,rowH);rowPanel.TabIndex=slot[rowIndex];});
   int top=0;
   int lineTop=top+Ui.Px(4),lineH=Ui.Px(28);
   var name=new Label{Text=entry.Name,Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft};
   rowPanel.Controls.Add(name);
   placers.Add(()=>name.Bounds=new Rectangle(Ui.Px(16),lineTop,colX-Ui.Px(20),lineH));
   var row=new CheckCell[Directions.Length];
   var fields=new TextBox[EdgeCount];
   var labels=new Label[EdgeCount];
   var chosen=new string[EdgeCount];
   Dictionary<string,string> entryRefs;
   if(!currentRefs.TryGetValue(entry.Id,out entryRefs))entryRefs=new Dictionary<string,string>();
   HashSet<string> allowed;
   if(!current.TryGetValue(entry.Id,out allowed))allowed=MainForm.DefaultEdgesFor(entry);
   Dictionary<string,int> gaps;
   if(!currentGaps.TryGetValue(entry.Id,out gaps))gaps=new Dictionary<string,int>();
   for(int i=0;i<Directions.Length;i++) {
    bool edge=i<EdgeCount;
    int index=i;
    var cell=new CheckCell{Checked=allowed.Contains(Directions[i])};
    row[i]=cell;
    rowPanel.Controls.Add(cell);
    placers.Add(()=>cell.Bounds=edge?new Rectangle(colLeft[index],lineTop,Ui.Px(36),lineH):new Rectangle(colLeft[index],lineTop,colW[index],lineH));
    if(!edge)continue;
    // Abstandsfeld erscheint nur bei angehakter Kante.
    var field=new Card{Size=new Size(Ui.Px(58),Ui.Px(26)),Padding=new Padding(Ui.Px(6),Ui.Px(4),Ui.Px(6),0)};
    placers.Add(()=>field.Location=new Point(colLeft[index]+Ui.Px(38),lineTop+(lineH-Ui.Px(26))/2));
    var box=new TextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Center,MaxLength=4,Font=Ui.UiFont,BackColor=Ui.Surface,ForeColor=Ui.TextColor};
    if(gaps.ContainsKey(Directions[i]))box.Text=gaps[Directions[i]].ToString();
    string cue=globalGap.ToString();
    box.HandleCreated+=(s,e)=>SendMessage(((TextBox)s).Handle,0x1501,new IntPtr(1),cue);
    box.KeyPress+=(s,e)=>{if(!Char.IsDigit(e.KeyChar)&&!Char.IsControl(e.KeyChar))e.Handled=true;};
    field.Controls.Add(box);
    // Bezug unter dem Feld: "am Rand" oder "an <Programm>"; Klick öffnet die Auswahl.
    string refId;
    if(entryRefs.TryGetValue(Directions[i],out refId)&&programs.Any(x=>String.Equals(x.Id,refId,StringComparison.OrdinalIgnoreCase)&&x.Id!=entry.Id))chosen[i]=refId;
    var refLabel=new Label{Font=Ui.SmallFont,ForeColor=Ui.Accent,BackColor=Ui.Surface,Tag="accent",AutoEllipsis=true,UseMnemonic=false,Cursor=Cursors.Hand,TextAlign=ContentAlignment.MiddleLeft};
    placers.Add(()=>refLabel.Bounds=new Rectangle(colLeft[index]+Ui.Px(4),lineTop+lineH,colW[index]-Ui.Px(8),Ui.Px(17)));
    var owner=entry;
    refLabel.Click+=(s,e)=>ShowRefMenu(owner,index,(Label)s);
    field.Visible=refLabel.Visible=cell.Checked;
    cell.CheckedChanged+=(s,e)=>{field.Visible=cell.Checked;refLabel.Visible=cell.Checked;UpdateCycles();};
    rowPanel.Controls.Add(field);rowPanel.Controls.Add(refLabel);
    fields[i]=box;labels[i]=refLabel;
   }
   var measure=new ModernButton("Messen",ButtonKind.Ghost,null);
   measure.Height=Ui.Px(28);measure.Margin=new Padding(0);
   placers.Add(()=>measure.Location=new Point(colX+colW.Sum()+(measureW-measure.Width)/2,lineTop+(lineH-measure.Height)/2));
   var measured=entry;
   measure.Click+=(s,e)=>Measure(measured);
   rowPanel.Controls.Add(measure);
   cells[entry.Id]=row;boxes[entry.Id]=fields;refs[entry.Id]=chosen;refLabels[entry.Id]=labels;
   for(int i=0;i<EdgeCount;i++)UpdateRefLabel(entry.Id,i);
  }
  card.Controls.Add(rows);card.Controls.Add(header);rows.BringToFront();
  Controls.Add(card);
  // Statuszeile neben "Standard" auf Höhe der Knöpfe (zwei Zeilen Platz, voller Text im Tooltip).
  status=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft};
  status.TextChanged+=(s,e)=>tips.SetToolTip(status,status.Text);
  Controls.Add(status);
  standard=new ModernButton("Standard",ButtonKind.Secondary,"");
  standard.Click+=(s,e)=>ResetToDefaults();
  ok=new ModernButton("Speichern",ButtonKind.Primary,null);
  cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  ok.Click+=(s,e)=>Accept();
  // Rote Fehlerzeile links neben den Knöpfen auf gleicher Höhe.
  error=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.ErrorColor,BackColor=Ui.Bg,Tag="error",AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleRight};
  // Ein Fehler hat Vorrang vor der Statuszeile (beide stehen zwischen "Standard" und "Abbrechen").
  error.TextChanged+=(s,e)=>status.Visible=error.Text.Length==0;
  Controls.Add(standard);Controls.Add(error);Controls.Add(cancel);Controls.Add(ok);
  AcceptButton=ok;CancelButton=cancel;
  // Starthöhe: Beschreibung, Tabelle, Statuszeile und Knöpfe; im Hauptfenster füllt die Seite den Inhaltsbereich.
  ClientSize=new Size(w,Ui.Px(4)+DescriptionHeight(inner)+Ui.Px(14)+card.Height+Ui.Px(16)+Ui.Px(38)+Ui.Px(20));
  Resize+=(s,e)=>Arrange();
  HandleCreated+=(s,e)=>{Arrange();PerformLayout();};
  Arrange();
  UpdateCycles();
  initialState=State();
 }
 public bool HasChanges{get{return initialState!=null&&State()!=initialState;}}
 string State() {
  var text=new StringBuilder();
  foreach(var entry in programs) {
   text.Append(entry.Id).Append(':');
   foreach(var cell in cells[entry.Id])text.Append(cell.Checked?'1':'0');
   foreach(var box in boxes[entry.Id])text.Append('|').Append(box==null?"":box.Text.Trim());
   foreach(var target in refs[entry.Id])text.Append('|').Append(target??"");
   text.Append(';');
  }
  return text.ToString();
 }
 // Gegenseitige Nachbarbezüge erkennen (z.B. Task-Manager links an TeamSpeak 3 und TeamSpeak 3 rechts an Task-Manager):
 // Das funktioniert, ist aber schwer zu durchschauen – beim Ausrichten zählt die Lage, die das andere Fenster gerade hat.
 // Bezüge innerhalb eines solchen Kreises werden orange markiert, die Statuszeile nennt die Programme.
 void UpdateCycles() {
  var graph=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in programs) {
   var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<EdgeCount;i++) {
    var neighbor=NeighborOf(refs[entry.Id][i]);
    if(neighbor!=null&&cells[entry.Id][i].Checked)targets.Add(neighbor.Id);
   }
   graph[entry.Id]=targets;
  }
  var reach=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var id in graph.Keys) {
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var open=new Stack<string>(graph[id]);
   while(open.Count>0) {
    string next=open.Pop();
    if(!seen.Add(next))continue;
    HashSet<string> more;
    if(graph.TryGetValue(next,out more))foreach(var m in more)open.Push(m);
   }
   reach[id]=seen;
  }
  var groups=new List<List<Entry>>();
  var grouped=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in programs) {
   if(grouped.Contains(entry.Id)||!reach[entry.Id].Contains(entry.Id))continue;
   var group=programs.Where(x=>reach[entry.Id].Contains(x.Id)&&reach[x.Id].Contains(entry.Id)).ToList();
   foreach(var x in group)grouped.Add(x.Id);
   groups.Add(group);
  }
  foreach(var entry in programs)
   for(int i=0;i<EdgeCount;i++) {
    var neighbor=NeighborOf(refs[entry.Id][i]);
    var owner=entry;
    bool inCycle=neighbor!=null&&cells[entry.Id][i].Checked&&groups.Any(g=>g.Contains(owner)&&g.Contains(neighbor));
    var label=refLabels[entry.Id][i];
    label.Tag=inCycle?"warning":"accent";
    label.ForeColor=inCycle?Ui.WarningColor:Ui.Accent;
   }
  string note="";
  if(groups.Count>0) {
   var parts=groups.Select(g=>NameList(g.Select(x=>"„"+x.Name+"“").ToList())).ToArray();
   bool pair=groups.All(g=>g.Count==2);
   note="Hinweis: "+String.Join("; ",parts)+(pair?" beziehen sich gegenseitig":" beziehen sich im Kreis aufeinander")+" (orange markiert). Beim Ausrichten zählt dann die Lage, die das andere Fenster gerade hat.";
  }
  if(status.Text.Length==0||status.Text==cycleNote)status.Text=note;
  cycleNote=note;
 }
 static string NameList(List<string> names) {
  if(names.Count<2)return names.Count==1?names[0]:"";
  return String.Join(", ",names.Take(names.Count-1).ToArray())+" und "+names[names.Count-1];
 }
 int DescriptionHeight(int width) {
  return TextRenderer.MeasureText(description.Text,description.Font,new Size(width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+Ui.Px(4);
 }
 // Breite und Höhe verteilen: Die Spalten wachsen mit (bis 1,5-fach), der Rest geht an die Programmspalte;
 // die Tabelle nimmt die Höhe zwischen Beschreibung und Knöpfen ein und scrollt bei Bedarf.
 // Selbst angeordnete Bildlauf-Seite (Globale Richtlinien): nicht anordnen, solange die Seite noch nicht im Fenster
 // sitzt oder das Fenster minimiert ist – sonst bleibt ein zu großer Bildlaufbereich mit leerer Fläche.
 bool ReadyToArrange() {
  if(!IsHandleCreated||ClientSize.Width<Ui.Px(400))return false;
  var top=TopLevelControl as Form;
  return top==null||top.WindowState!=FormWindowState.Minimized;
 }
 void Arrange() {
  if(!ReadyToArrange())return;
  int pad=Ui.Px(24),inner=Math.Max(Ui.Px(400),ClientSize.Width-2*pad);
  int y=Ui.Px(4);
  int descriptionHeight=DescriptionHeight(inner);
  description.Bounds=new Rectangle(pad,y,inner,descriptionHeight);
  y+=descriptionHeight+Ui.Px(14);
  int buttonsTop=ClientSize.Height-Ui.Px(20)-Ui.Px(38);
  int cardHeight=Math.Max(head+rowH+Ui.Px(10),buttonsTop-Ui.Px(16)-y);
  card.Bounds=new Rectangle(pad,y,inner,cardHeight);
  buttonsTop=card.Bottom+Ui.Px(16);
  standard.Location=new Point(pad,buttonsTop);
  ok.Location=new Point(pad+inner-ok.Width,buttonsTop);
  // Globale Richtlinien, Einstellungsseiten: "Abbrechen" links, "Speichern" als Primärknopf ganz rechts, 10 Abstand.
  cancel.Location=new Point(ok.Left-Ui.Px(10)-cancel.Width,buttonsTop);
  // Statuszeile und rote Fehlerzeile teilen sich den Platz zwischen "Standard" und "Abbrechen".
  var line=new Rectangle(standard.Right+Ui.Px(16),buttonsTop,Math.Max(0,cancel.Left-Ui.Px(16)-standard.Right-Ui.Px(16)),ok.Height);
  status.Bounds=line;
  error.Bounds=line;
  // Spalten: Grundbreiten 124 (Kanten), 70 (Mitte), 96 (Messen); Programmspalte mindestens 220.
  float baseSum=4*124+2*70+96;
  int available=inner-Ui.Px(14)-Ui.Px(220);
  float factor=Math.Max(1f,Math.Min(1.5f,available/(baseSum*Ui.S)));
  for(int i=0;i<colW.Length;i++)colW[i]=(int)Math.Round((i<EdgeCount?124:70)*factor*Ui.S);
  measureW=(int)Math.Round(96*factor*Ui.S);
  // Platz für die senkrechte Bildlaufleiste der Tabelle freihalten.
  tableWidth=inner-Ui.Px(2)-SystemInformation.VerticalScrollBarWidth;
  colX=tableWidth-Ui.Px(12)-colW.Sum()-measureW;
  for(int i=0,x=colX;i<colW.Length;x+=colW[i],i++)colLeft[i]=x;
  // In einer gescrollten Tabelle gelten Positionen relativ zur Ansicht: erst nach oben, dann zurück.
  int scroll=-rows.AutoScrollPosition.Y;
  rows.AutoScrollPosition=new Point(0,0);
  rows.SuspendLayout();header.SuspendLayout();
  foreach(var place in placers)place();
  header.ResumeLayout();rows.ResumeLayout();
  rows.AutoScrollPosition=new Point(0,scroll);
 }
 Entry NeighborOf(string id){return id==null?null:programs.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));}
 void UpdateRefLabel(string id,int index) {
  var neighbor=NeighborOf(refs[id][index]);
  refLabels[id][index].Text=neighbor==null?"am Rand ▾":"an "+neighbor.Name+" ▾";
 }
 // Auswahl, wovon der Abstand gilt: Bildschirmrand oder ein anderes Programm der Liste.
 void ShowRefMenu(Entry entry,int index,Label anchor) {
  MenuStyle.Prepare(refMenu);
  refMenu.Items.Add(MenuStyle.TopSpacer());
  string current=refs[entry.Id][index];
  refMenu.Items.Add(MenuStyle.Choice("Bildschirmrand",current==null,(s,a)=>{refs[entry.Id][index]=null;UpdateRefLabel(entry.Id,index);UpdateCycles();}));
  refMenu.Items.Add(new ToolStripSeparator());
  string side=Directions[index]=="left"?"rechts neben":Directions[index]=="right"?"links neben":Directions[index]=="top"?"unter":"über";
  foreach(var other in programs) {
   if(other.Id==entry.Id)continue;
   var target=other;
   bool isCurrent=String.Equals(current,other.Id,StringComparison.OrdinalIgnoreCase);
   refMenu.Items.Add(MenuStyle.Choice(side+" "+other.Name,isCurrent,(s,a)=>{refs[entry.Id][index]=target.Id;UpdateRefLabel(entry.Id,index);UpdateCycles();}));
  }
  refMenu.Items.Add(MenuStyle.BottomSpacer());
  MenuStyle.ShowOnScreen(refMenu,anchor,new Point(0,anchor.Height),ToolStripDropDownDirection.BelowRight);
 }
 // Aktuelle Abstände des offenen Fensters in die Felder der angehakten Kanten übernehmen.
 void Measure(Entry entry) {
  var row=cells[entry.Id];var fields=boxes[entry.Id];var chosen=refs[entry.Id];
  Dictionary<string,int> m;
  Rectangle own;
  try {m=VisibleAlignment.MeasureGaps(entry);}
  catch(Exception ex){status.Text=ex.Message;return;}
  VisibleAlignment.TryVisibleRect(entry,out own);
  var taken=new List<string>();
  for(int i=0;i<EdgeCount;i++) {
   if(!row[i].Checked)continue;
   int measured=m[Directions[i]];
   string suffix="";
   Entry neighbor=NeighborOf(chosen[i]);
   if(neighbor!=null) {
    // Abstand zur gegenüberliegenden Kante des Nachbarprogramms.
    Rectangle r;
    if(!VisibleAlignment.TryVisibleRect(neighbor,out r)){taken.Add(Titles[i]+": "+neighbor.Name+" ist nicht offen");continue;}
    measured=Directions[i]=="left"?own.Left-r.Right:Directions[i]=="right"?r.Left-own.Right:Directions[i]=="top"?own.Top-r.Bottom:r.Top-own.Bottom;
    suffix=" bis "+neighbor.Name;
    if(measured<0)suffix+=" (überlappt, 0 gesetzt)";
   }
   int value=Math.Max(0,Math.Min(MainForm.MaxEdgeGap,measured));
   fields[i].Text=value.ToString();
   taken.Add(Titles[i]+" "+value+" px"+suffix);
  }
  string all="oben "+m["top"]+", unten "+m["bottom"]+", links "+m["left"]+", rechts "+m["right"]+" px";
  status.Text=taken.Count>0?entry.Name+" gemessen ("+all+" zum Rand) – übernommen: "+String.Join(", ",taken.ToArray())+".":entry.Name+" gemessen ("+all+"). Keine Kante angehakt – zuerst die Kanten anhaken, die übernommen werden sollen.";
 }
 void ResetToDefaults() {
  foreach(var entry in programs) {
   var allowed=MainForm.DefaultEdgesFor(entry);
   var row=cells[entry.Id];
   for(int i=0;i<Directions.Length;i++)row[i].Checked=allowed.Contains(Directions[i]);
   foreach(var box in boxes[entry.Id])box.Text="";
   for(int i=0;i<EdgeCount;i++){refs[entry.Id][i]=null;UpdateRefLabel(entry.Id,i);}
  }
  status.Text="";
  UpdateCycles();
  status.Text="Vorgaben wiederhergestellt (erst mit „Speichern“ übernommen).";
 }
 void Accept() {
  error.Text="";
  var rules=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  var allGaps=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  var allRefs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in programs) {
   var set=new HashSet<string>();
   var gaps=new Dictionary<string,int>();
   var row=cells[entry.Id];var fields=boxes[entry.Id];
   for(int i=0;i<Directions.Length;i++) {
    if(!row[i].Checked)continue;
    set.Add(Directions[i]);
    if(i>=EdgeCount)continue;
    string text=fields[i].Text.Trim();
    if(text.Length==0)continue;
    int value;
    // Erst alles prüfen, dann speichern.
    if(!Int32.TryParse(text,out value)||value<0||value>MainForm.MaxEdgeGap) {
     error.Text=entry.Name+", "+Titles[i]+": Abstand 0 bis "+MainForm.MaxEdgeGap+" px";
     fields[i].Focus();fields[i].SelectAll();
     return;
    }
    gaps[Directions[i]]=value;
   }
   var entryRefs=new Dictionary<string,string>();
   for(int i=0;i<EdgeCount;i++)if(row[i].Checked&&refs[entry.Id][i]!=null)entryRefs[Directions[i]]=refs[entry.Id][i];
   rules[entry.Id]=set;allGaps[entry.Id]=gaps;allRefs[entry.Id]=entryRefs;
  }
  Rules=rules;Gaps=allGaps;Refs=allRefs;
  if(Save==null){DialogResult=DialogResult.OK;Close();return;}
  string message=Save();
  if(message!=null){error.Text=message;return;}
  // Globale Richtlinien, Einstellungsseiten: Speichern schließt die Seite nicht, sondern zeigt eine kurze grüne Meldung.
  initialState=State();
  ShowSaved();
 }
 const string SavedText="Gespeichert – gilt ab jetzt für „Wiederherstellen“ und das Ausrichten.";
 System.Windows.Forms.Timer savedTimer;
 void ShowSaved() {
  status.Tag="success";
  status.ForeColor=Ui.SuccessColor;
  status.Text=SavedText;
  if(savedTimer==null) {
   savedTimer=new System.Windows.Forms.Timer{Interval=4000};
   savedTimer.Tick+=(s,e)=>{
    savedTimer.Stop();
    status.Tag="muted";
    status.ForeColor=Ui.Muted;
    if(status.Text==SavedText)status.Text=cycleNote;
   };
   FormClosed+=(s,e)=>savedTimer.Dispose();
  }
  savedTimer.Stop();
  savedTimer.Start();
 }
}
// Einzelnes Häkchen (wie in der Programmliste), anklickbar und per Leertaste umschaltbar.
public class CheckCell:Control {
 bool isChecked;
 public CheckCell() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  BackColor=Ui.Surface;Cursor=Cursors.Hand;TabStop=true;
 }
 public event EventHandler CheckedChanged;
 public bool Checked{get{return isChecked;}set{if(isChecked==value)return;isChecked=value;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button==MouseButtons.Left){Focus();Checked=!Checked;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  int size=Ui.Px(18);
  var box=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
  ProgramList.DrawCheck(g,box,isChecked,Enabled);
  if(Focused&&ShowFocusCues)
   using(var path=Ui.Round(Rectangle.Inflate(box,Ui.Px(3),Ui.Px(3)),Ui.Px(5)))
   using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Surface,0.3f))) g.DrawPath(pen,path);
 }
}
}
