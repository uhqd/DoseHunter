

<img src="fisherMan4.png" width="400" align="left">
<br clear="left"/>


# DOSE HUNTER REPOSITORY

## Introduction
DoseHunter is a stand-alone executable for Varian Eclipse V18.x. (or more) DoseHunter automatically collects dose data (dose max, dose min, D95\%, etc.) for a large number of patients in your database. DoseHunter creates a .csv file with your data that can be easily analyzed with Excel, Python...
It is possible to get data from simple plans, sum plans but not uncertainties plans (since V18.0).

## Documentation

 Please read the [user guide](https://github.com/uhqd/DoseHunter/blob/master/myDoseHunter/Dose%20Hunter%20-%20USERGUIDE.pdf). (also in [french](https://github.com/uhqd/DoseHunter/blob/master/myDoseHunter/Dose_Hunter_User_Guide__french_.pdf))


## Authors

**[Luc Simon](https://github.com/uhqd/)**: Project initiator and lead developer. Conceived the original idea, designed the architecture, and implemented the entire core codebase of DoseHunter. 

**[François-Xavier Arnaud](https://github.com/fxarnaud/)**: Provided assistance on regular expression patterns.

**[Bradley Beeksma](https://github.com/BradBeeksma)**: Implementation of additional metrics (Paddick index, monitor units, etc.).



**[Killian Lacaze](https://github.com/lacazek)** and **[Farzam Fayah](https://github.com/Farzam07)**: added the plan uncertainties analysis on V15 but it doesn't work on v18. We are very interested if you are able to get data from plan uncertainty, using a stand alone script


## To cite Dose Hunter

If you like and use Dose Hunter, do not hesitate to tell us. 
You can cite it in your publications.
This helps acknowledge the software and supports its continued development.

**Citation for LaTeX users (BibTeX)**

For Latex users, update your .bib file and add the content of this [file](https://github.com/uhqd/DoseHunter/blob/master/dosehunter.bib).
Then cite it in your LaTeX document using:
> \cite{DoseHunter2025}


**Citation for Zotero, EndNote, Mendeley (RIS format)**

You can import this RIS [file](https://github.com/uhqd/DoseHunter/blob/master/dosehunter.ris) directly into Zotero, EndNote, or similar reference managers


**Simple Aknowledgements**

You can also simply add in the "acknowledgement" section of your articles or your slides:

_This work was done using Dose Hunter, a free and open-source tool to extract data from Varian Aria (github.com/uhqd/dosehunter)_




