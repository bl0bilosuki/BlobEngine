-- BlobEngine 7.5 dashboard for Cheat Engine's embedded Lua engine.
-- Searches real Address List / CT records only; never guesses arbitrary addresses.
local C={bg=0x171622,panel=0x211F30,field=0x29263B,accent=0x873BEB,accent2=0xA45CFF,text=0xF0ECFA,muted=0xB8B0D2,green=0x32D36B}
local aliases={
 ["доллары"]={"деньги","money","cash","wallet","dollar","dollars","player money","общие деньги"},
 ["деньги"]={"доллары","money","cash","wallet","dollar","dollars","player money"},
 ["здоровье"]={"health","hp","hit points","жизни"},["броня"]={"armor","armour"},
 ["патроны"]={"ammo","ammunition","bullets","clip"},["уровень"]={"level","xp level"},
 ["опыт"]={"experience","xp"},["выносливость"]={"stamina","energy"},["скорость"]={"speed","movement speed"}}
local function norm(s) return tostring(s or ""):lower():gsub("[%c]"," "):gsub("%s+"," ") end
local function makeTerms(q)
 local terms={norm(q)}; q=norm(q)
 for key,values in pairs(aliases) do
  if q==norm(key) then for _,v in ipairs(values) do terms[#terms+1]=norm(v) end end
  for _,v in ipairs(values) do if q==norm(v) then terms[#terms+1]=norm(key); for _,o in ipairs(values) do terms[#terms+1]=norm(o) end; break end end
 end
 return terms
end
local f=createForm(); f.Caption="BlobEngine 7.5"; f.Width=1100; f.Height=760; f.Position="poScreenCenter"; f.Color=C.bg
-- Replace the stock main window while keeping Cheat Engine as the live memory backend.
if MainForm then MainForm.hide() end
f.OnClose=function() if MainForm then MainForm.show() end; return caFree end
f.Font.Name="Segoe UI"; f.Font.Size=10; f.Font.Color=C.text
local h=createPanel(f); h.Align="alTop"; h.Height=64; h.BevelOuter="bvNone"; h.Color=C.panel
local logo=createLabel(h); logo.Caption="B"; logo.Left=18; logo.Top=7; logo.Width=42; logo.Height=48; logo.Font.Size=30; logo.Font.Style="[fsBold]"; logo.Font.Color=C.accent2
local title=createLabel(h); title.Caption="BlobEngine"; title.Left=68; title.Top=17; title.Width=240; title.Height=30; title.Font.Size=18; title.Font.Style="[fsBold]"; title.Font.Color=C.text
local top=createPanel(f); top.Align="alTop"; top.Height=100; top.BevelOuter="bvNone"; top.Color=C.bg
local gl=createLabel(top); gl.Caption="Игровой профиль"; gl.Left=18; gl.Top=9; gl.Font.Color=C.muted
local game=createComboBox(top); game.Left=18; game.Top=34; game.Width=250; game.Color=C.field; game.Font.Color=C.text
for _,v in ipairs({"Автоопределение","GTA V","Cyberpunk 2077","Minecraft","Пользовательский профиль"}) do game.Items.add(v) end; game.ItemIndex=0
local pl=createLabel(top); pl.Caption="Процесс Windows"; pl.Left=292; pl.Top=9; pl.Font.Color=C.muted
local proc=createComboBox(top); proc.Left=292; proc.Top=34; proc.Width=390; proc.Color=C.field; proc.Font.Color=C.text
local refresh=createButton(top); refresh.Caption="Обновить"; refresh.Left=694; refresh.Top=32; refresh.Width=88
local attach=createButton(top); attach.Caption="Подключить"; attach.Left=790; attach.Top=32; attach.Width=96
local sp=createPanel(f); sp.Align="alTop"; sp.Height=76; sp.BevelOuter="bvNone"; sp.Color=C.panel
local sl=createLabel(sp); sl.Caption="Поиск по названию параметра"; sl.Left=18; sl.Top=7; sl.Font.Color=C.muted
local q=createEdit(sp); q.Left=18; q.Top=32; q.Width=690; q.Text="доллары"; q.Color=C.field; q.Font.Color=C.text
local find=createButton(sp); find.Caption="Найти"; find.Left=720; find.Top=29; find.Width=165
local rp=createPanel(f); rp.Align="alClient"; rp.BevelOuter="bvNone"; rp.Color=C.bg
local list=createListBox(rp); list.Align="alClient"; list.ItemHeight=28; list.Color=C.field; list.Font.Color=C.text
local foot=createPanel(f); foot.Align="alBottom"; foot.Height=105; foot.BevelOuter="bvNone"; foot.Color=C.panel
local vl=createLabel(foot); vl.Caption="Новое значение"; vl.Left=18; vl.Top=12; vl.Font.Color=C.muted
local value=createEdit(foot); value.Left=18; value.Top=38; value.Width=300; value.Text="10000"; value.Color=C.field; value.Font.Color=C.text
local change=createButton(foot); change.Caption="Изменить выбранное"; change.Left=332; change.Top=36; change.Width=210
local status=createLabel(foot); status.Left=560; status.Top=40; status.Width=325; status.Caption="Готово. Загрузите таблицу .CT."; status.Font.Color=C.green
local pids,records={},{}
local function refreshProcesses()
 proc.Items.clear(); pids={}
 for pid,name in pairs(getProcesslist()) do local n=tonumber(pid); if n then pids[#pids+1]=n; proc.Items.add(string.format("%s [PID %d]",tostring(name),n)) end end
 local current=getOpenedProcessID(); status.Caption=(current and current~=0) and ("Подключён PID "..current) or "Выберите процесс и нажмите Подключить."
end
local function search()
 list.Items.clear(); records={}; local al=getAddressList(); local terms=makeTerms(q.Text)
 for i=0,al.Count-1 do local mr=al.getMemoryRecord(i); local d=norm(mr.Description); local ok=false
  for _,t in ipairs(terms) do if t~="" and d:find(t,1,true) then ok=true; break end end
  if ok then records[#records+1]=mr; list.Items.add(string.format("%s | %s | %s",tostring(mr.Description),tostring(mr.Value),tostring(mr.Type))) end
 end
 status.Caption=#records>0 and ("Найдено записей: "..#records.." в Address List / .CT") or "Совпадений нет. Загрузите профиль или .CT."
end
refresh.OnClick=refreshProcesses
attach.OnClick=function() local i=proc.ItemIndex; if i<0 or not pids[i+1] then status.Caption="Сначала выберите процесс."; return end; local ok,e=pcall(openProcess,pids[i+1]); status.Caption=ok and ("Подключение к PID "..pids[i+1]) or tostring(e) end
find.OnClick=search
q.OnKeyPress=function(sender,key) if key==13 then search(); key=0 end end
change.OnClick=function() local i=list.ItemIndex; if i<0 or not records[i+1] then status.Caption="Выберите запись."; return end; local mr=records[i+1]; local ok,e=pcall(function() mr.Value=value.Text end); status.Caption=ok and ("Изменено: "..mr.Description) or ("Ошибка: "..tostring(e)); if ok then search() end end
list.OnDblClick=function() local i=list.ItemIndex; if records[i+1] then value.Text=tostring(records[i+1].Value) end end
game.OnChange=function() status.Caption="Профиль "..game.Text..". Для реальных адресов нужна совместимая .CT." end
refreshProcesses()
