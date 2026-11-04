# PrimEx Smart Save — instalimi për stafin

1. Kopjo `PrimEx.SmartSave-0.2.13-win-x64.msi` në kompjuterin e punonjësit.
2. Nëse është hapur versioni i vjetër, mbylle nga ikona pranë orës me **Exit**.
3. Hape MSI-në me llogarinë normale të punonjësit dhe përfundo instalimin.
   .NET është i përfshirë; nuk duhet instaluar veçmas. Paketa është për Windows x64.
4. Hap shortcut-in **PrimEx Smart Save** në Desktop.
5. Në konfigurimin e parë, aktivizo **Enable automatic guidance**, zgjidh
   Downloads dhe folderët e tjerë që duhen monitoruar, pastaj zgjidh folderin
   e autorizuar të kompanisë. Mbaje të shënuar **Start automatically when I sign
   in to Windows** dhe kliko **Apply automatic guidance**.
   **All completed file types** është i aktivizuar si parazgjedhje, përfshirë
   PNG/JPG dhe formatet e tjera; nuk duhet t'i shtoni një nga një.
6. Shkarko një PDF provë në Downloads. Pasi të përfundojë dhe të lirohet nga
   aplikacioni, duhet të shfaqet pyetja për vendosjen e dokumentit.
7. Dil nga llogaria Windows dhe hyr përsëri: ikona duhet të jetë pranë orës,
   pa pasur nevojë që ta hapësh manualisht çdo ditë.

**X e fsheh aplikacionin; Exit e ndalon.** Nëse përdoret Exit ose aplikacioni
pëson gabim, hapet sërish manualisht ose në hyrjen tjetër në Windows. Nuk është
shërbim që punon pa përdorues të kyçur dhe nuk ka watchdog për rinisje pas crash-it.

Konfigurimi bëhet një herë për secilën llogari. Lejet ekzistuese të serverit
mbeten në fuqi. Dokumentet nuk lëvizen pa konfirmim. Aplikacioni sugjeron vendosjen
pas ruajtjes; ndërhyrja para Save në Word/Excel nuk është implementuar ende.

Në kompjuterët me version të vjetër, mbylleni me Exit dhe instaloni 0.2.13
me të njëjtën llogari Windows. Folderët dhe serveri ruhen. Lista standarde e
vjetër e formateve kalon automatikisht në të gjitha llojet; filtrat e personalizuar
ruhen dhe mund të ndryshohen te Automatic guidance. Për test shkarkoni një file
të ri, jo një file që ishte ruajtur para aktivizimit.

Pas **Files PX**, hapet dritarja standarde e Windows-it për dosjet.
Gjeni dosjen në server, klikoni **Select Folder**, pastaj zgjidhni **Move to Files PX**
ose **Copy to Files PX — keep original** dhe konfirmoni me **Move here** / **Copy here**. **Cancel** e lë dokumentin aty ku është. Butoni **Downloads** shfaqet
vetëm në pyetjen e parë dhe e lë skedarin në dosjen lokale ku është ruajtur.

Butonat **Files PX** dhe **Downloads** janë në të njëjtin rresht. Pas klikimit
**Downloads** shfaqet paralajmërimi: **Kujdes: Vetëm për dokumente
provë dhe kopje jo të rëndësishme. Dokumentet e rëndësishme të punës ruajini në Files PX.**

Kliko **Vazhdo me Downloads** për ta mbajtur skedarin lokalisht, ose **Kthehu**
për të zgjedhur Files PX. Zgjedhja mbahet mend vetëm pas konfirmimit.

Në versionin 0.2.8, kur riruani një dokument ekzistues me **Save**, nuk shfaqet
pyetja për vendndodhjen. Dokumenti ruhet aty ku është hapur, edhe pas rinisjes së
programit dhe kur aplikacioni e zëvendëson skedarin gjatë ruajtjes. Kjo vlen edhe
nëse pyetja e parë është mbyllur pa zgjedhur Downloads. **Save As** me emër ose
dosje tjetër krijon një skedar të ri dhe mund të shfaqë pyetjen e parë.
Ndryshimet në kopje lokale nuk dërgohen automatikisht te kopja në server.

Versioni 0.2.9 optimizon hapjen. Dritarja hapet para leximit të historisë dhe
përgatitjes së monitorimit, të cilat kryhen në sfond. Pritni që statusi të tregojë
**Automatic guidance ON** para testit me një skedar të ri. Nëse hapja mbetet e
ngadaltë, përdorni **Export diagnostics…**: raporti përmban versionin, matjet e
nisjes dhe përdorimin e memories/CPU-së së programit, pa emra apo përmbajtje dokumentesh.

Në versionin 0.2.10, projektet e programimit përjashtohen automatikisht. Skedarët
që krijon Codex ose VS Code brenda një projekti Git/VS Code apo një projekti tjetër
të njohur nuk hapin pyetjen e ruajtjes, përfshirë dokumentet dhe figurat brenda tij.
Te **Automatic guidance**, mbajeni aktiv **Skip coding projects automatically**.
Për një dosje që nuk njihet, klikoni **Exclude a folder…**, zgjidhni projektin ose
dosjen **Projektet**, pastaj **Apply automatic guidance**. Përjashtimi vlen edhe
për të gjithë nënfolderët. Dokumentet jashtë këtyre dosjeve vazhdojnë normalisht.

Në 0.2.11, te Automatic guidance klikoni **Connect VS Code / Visual Studio…**.
Instaloni lidhjen për secilin editor vetëm një herë. Rihapni dritaret e VS Code,
ose përfundoni instaluesin e shtesës dhe rinisni Visual Studio. Mbajeni aktiv
**Skip coding projects automatically**. Projektet/dosjet lokale që hapni më pas
regjistrohen automatikisht, edhe pa Git. Visual Studio kërkon 2022 17.14+ ose 2026,
Windows x64. Hapni dosjen konkrete të projektit, jo të gjithë Desktop-in.
Aktivizimi brenda editorëve realë kërkon ende verifikim në kompjuterin pilot.

Kjo është paketë pilot pa nënshkrim digjital. IT duhet ta verifikojë/nënshkruajë
sipas rregullave të kompanisë; mos anashkalo bllokimet e sigurisë. Testojeni në një
kompjuter përpara shpërndarjes së përgjithshme. Për çinstalim përdor Windows
Settings → Apps → Installed apps → PrimEx Smart Save → Uninstall. Konfigurimi,
operacionet në pritje dhe dokumentet ruhen.


Në 0.2.12, ruajtjet dhe shkarkimet nuk e hapin më dritaren kryesore pas pyetjes.
Kur programi është në tray ose i minimizuar, shfaqet vetëm dritarja e destinacionit.
Pas zgjedhjes ose mbylljes së saj, dritarja kryesore mbetet e fshehur/minimizuar.
Për historinë dhe operacionet përdorni Open Smart Save nga ikona pranë orës.

Në 0.2.13, **Move** e largon origjinalin vetëm pasi kopja në Files PX verifikohet.
**Copy** e mban origjinalin në Downloads ose në dosjen ku ishte dhe ruan një kopje
në Files PX. Zgjedhja shënohet në histori dhe ruhet për riprovim pas gabimit.
Ndryshimet e mëvonshme në origjinal nuk përditësojnë automatikisht kopjen në server.
