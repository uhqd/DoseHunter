
<img src="fisherMan4.png" width="500" align="right">


# DOSE HUNTER


DoseHunter is a stand-alone executable for Varian Eclipse V18.x. (or more) DoseHunter automatically collects dose data (dose max, dose min, D95\%, etc.) for a large number of patients in your database. DoseHunter creates a .csv file with your data that can be easily analyzed with Excel, Python...
It is possible to get data from simple plans, sum plans but not uncertainties plans (since V18.0).

 Please read the [user guide](https://github.com/uhqd/DoseHunter/blob/master/myDoseHunter/Dose%20Hunter%20-%20USERGUIDE.pdf). (also in [french](https://github.com/uhqd/DoseHunter/blob/master/myDoseHunter/Dose_Hunter_User_Guide__french_.pdf))

# To cite Dose Hunter
If you like and use Dose Hunter, do not hesitate to tell us. 
You can cite it in your publications.
This helps acknowledge the software and supports its continued development.
## Citation for LaTeX users (BibTeX)
For Latex users, update your .bib file and add the content of this [file](https://github.com/uhqd/DoseHunter/dosehunter.bib).
Then cite it in your LaTeX document using:
> \cite{DoseHunter2025}


## Citation for Zotero, EndNote, Mendeley (RIS format)
You can import this RIS [file](https://github.com/uhqd/DoseHunter/dosehunter.ris) directly into Zotero, EndNote, or similar reference managers


## Simple Aknowledgements

You can also simply add in the "acknowledgement" section of your articles or your slides:

**This work was done using Dose Hunter, a free and open-source tool to extract data from Varian Aria (github.com/uhqd/dosehunter)**


# Authors

**[Luc Simon](https://github.com/uhqd/)<sup>1</sup>, [François-Xavier Arnaud](https://github.com/fxarnaud/)<sup>1</sup>, [Bradley Beeksma](https://github.com/BradBeeksma)<sup>2</sup>, [Killian Lacaze](https://github.com/lacazek), [Farzam Fayah](https://github.com/Farzam07)<sup>1</sup>**


(1)[IUCT-Oncopole](https://www.iuct-oncopole.fr/), Toulouse, France

(2)[Calvary Mater Newcastle](https://www.calvarycare.org.au/public-hospital-mater-newcastle/), New Castle, Australia


- L. Simon: all the code.
- F.-X. Arnaud: help for the 'oh my god' regex 
- B. Beeksma: add the Paddick, MU, ... 
- K. Lacaze: add the plan uncertainties analysis
- F. Sayah: help KL finding the bug on v15... but it doesn't work on v18. Very interested if you are able to get data from plan uncertainty, using a stand alone script



