-- BlobEngine named search over existing Address List / .CT records.
local aliases={["доллары"]={"dollars","money","cash","wallet","деньги"},["деньги"]={"money","cash","wallet","dollars"},["здоровье"]={"health","hp","жизни"},["броня"]={"armor","armour"},["патроны"]={"ammo","ammunition","bullets"},["уровень"]={"level","xp level"},["опыт"]={"experience","xp"}}
local function norm(s) return tostring(s or ""):lower():gsub("[%c]"," "):gsub("%s+"," ") end
local function terms(q) local t={norm(q)}; for k,v in pairs(aliases) do if norm(q)==norm(k) then for _,x in ipairs(v) do t[#t+1]=norm(x) end end end; return t end
local f=createForm(); f.Caption="BlobEngine — Поиск по названиям"; f.Width=760; f.Height=500; f.Position="poScreenCenter"
local q=createEdit(f); q.Left=12; q.Top=12; q.Width=570; q.Text="доллары"
local b=createButton(f); b.Left=594; b.Top=10; b.Width=145; b.Height=32; b.Caption="Найти"
local l=createListBox(f); l.Left=12; l.Top=52; l.Width=727; l.Height=320
local v=createEdit(f); v.Left=12; v.Top=390; v.Width=430; v.Text="10000"
local a=createButton(f); a.Left=455; a.Top=388; a.Width=284; a.Height=34; a.Caption="Изменить выбранное"
local s=createLabel(f); s.Left=12; s.Top=435; s.Width=727; s.Caption="Источник: текущая Address List / .CT"
local found={}
local function search() l.Items.clear(); found={}; local al=getAddressList(); local ts=terms(q.Text); for i=0,al.Count-1 do local mr=al.getMemoryRecord(i); local d=norm(mr.Description); for _,t in ipairs(ts) do if t~="" and d:find(t,1,true) then found[#found+1]=mr; l.Items.add(mr.Description.." | "..tostring(mr.Value).." | "..tostring(mr.Type)); break end end end; s.Caption="Найдено: "..#found.." записей (Address List / .CT)" end
b.OnClick=search; q.OnKeyPress=function(sender,key) if key==13 then search(); key=0 end end
a.OnClick=function() local i=l.ItemIndex; if i<0 or not found[i+1] then s.Caption="Сначала выберите запись."; return end; found[i+1].Value=v.Text; search() end
l.OnDblClick=function() local i=l.ItemIndex; if found[i+1] then v.Text=tostring(found[i+1].Value) end end
search()
