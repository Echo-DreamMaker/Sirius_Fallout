misfits-ghoul-feral-complete = Твой разум поддался звериному инстинкту!
misfits-ghoul-feral-critical = Ты двигаешься как во сне. Всё в тумане. Скоро всё будет хорошо.
misfits-ghoul-feral-critical-others = яростно бьётся в конвульсиях и ревёт!
misfits-ghoul-feral-danger1 = Твои мышцы яростно дёргаются.
misfits-ghoul-feral-danger1-others = внезапно дёргается.
misfits-ghoul-feral-danger2 = Ты невольно рычишь.
misfits-ghoul-feral-danger2-others = дёргается и рычит!
misfits-ghoul-feral-danger3 = Тебя охватывает всепоглощающее чувство ужаса.
misfits-ghoul-feral-examine = { CAPITALIZE(SUBJECT($target)) } {CONJUGATE-BE($target)} непроизвольно дёргается.
misfits-ghoul-feral-warning = Твои мышцы на миг сокращаются.
reagent-effect-guidebook-modify-feralization = Снижает счётчик феррализации у гуля, который превращается в ферала ({ $chance ->
  [1] всегда
  *[other] { $chance } шанс
  { $delta } счётчик феррализации.
   Эффективно при значении { $threshold } и выше.
}),