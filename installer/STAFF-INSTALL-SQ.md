# PrimEx Smart Save — instalimi për stafin

1. Kopjo `PrimEx.SmartSave-0.2.0-win-x64.msi` në kompjuterin e punonjësit.
2. Nëse është hapur versioni i vjetër, mbylle nga ikona pranë orës me **Exit**.
3. Hape MSI-në me llogarinë normale të punonjësit dhe përfundo instalimin.
   .NET është i përfshirë; nuk duhet instaluar veçmas. Paketa është për Windows x64.
4. Hap shortcut-in **PrimEx Smart Save** në Desktop.
5. Në konfigurimin e parë, aktivizo **Enable automatic guidance**, zgjidh
   Downloads dhe folderët e tjerë që duhen monitoruar, pastaj zgjidh folderin
   e autorizuar të kompanisë. Mbaje të shënuar **Start automatically when I sign
   in to Windows** dhe kliko **Apply automatic guidance**.
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

Kjo është paketë pilot pa nënshkrim digjital. IT duhet ta verifikojë/nënshkruajë
sipas rregullave të kompanisë; mos anashkalo bllokimet e sigurisë. Testojeni në një
kompjuter përpara shpërndarjes së përgjithshme. Për çinstalim përdor Windows
Settings → Apps → Installed apps → PrimEx Smart Save → Uninstall. Konfigurimi,
operacionet në pritje dhe dokumentet ruhen.
