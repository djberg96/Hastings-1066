# Hastings-1066
This is a Recreation of the *Hastings: 1066* rulebook from Strategy & Tactics Magazine, Nr. 110.

Custom color unit images replace the original black & white images from the rulebook. They are very similar but not quite identical. Some other images, such as facing and missile fire, have not yet been added. However, I have added several charts directly into the rules where I felt it added some value. I suspect that most of the charts were left out of the original rules (they say "see map") because of the magazine format rather than any real artistic objection.

Other than fixing one or two minor typos, consistent spelling of "Housecarl", and inclusion of the official errata and one or two personal comments, this text is mostly identical to the magazine rules. I did make a minor numbering scheme change that follows more modern rulebooks where a sub-subsection like e.g. 3.31 is instead numbered 3.3.1, but that's about it.

https://boardgamegeek.com/boardgame/6021/hastings-1066

I've uploaded a PDF, but there's a chance it's out of date. To generate the rules PDF from source, run `cd Documents` and then run `pdflatex --jobname=Hastings_1066 main.tex` twice. This creates `Documents/Hastings_1066.pdf`; the second pass refreshes the table of contents.

## Computer game

The solo Norman-versus-Saxon Unity implementation is in [UnityProject](UnityProject/README.md). Open that project with Unity 6000.3.6f1; its main scene is `Assets/Scenes/Hastings.unity`. The generated Mac app is in `Builds/Mac/Hastings 1066.app` in this workspace.

Installing pdflatex will depend on your operating system. I leave that as an exercise to the reader.
