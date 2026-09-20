# Sharpmines
<p align="center">
  <img src="images/sharpmines.png" alt="Minesweeper screenshot" width="600">
  <br>
  <em>Sharpmines running in the terminal</em>
</p>

Sharpmines is a simple minesweeper game built with .NET 10. It's visuals are powered exclusively by ansi escape sequences. The game Is built to look similar to the minesweeper from google we all *surely* all know and love, wich you can find [here](https://www.google.com/fbx?fbx=minesweeper).

## Features

The game has some QOL features, including: 
- flood fill
- [chording](https://minesweeper.online/help/gameplay)
- fast replay

and other nice features, like the ability to set games with custom board sizes, and arbitraty mine counts.

## Controls

What's better, the game *partially* supports vim motions for movement, so you don't have to take your fingers of the home row *(or, god forbid, touch the mouse)* to play.

The game's controlls are:
| control       | action      |
| ------------- | ----------- |
| HJKL / arrows | move around |
| D / Enter     | dig         |
| F / M         | toggle flag |
| Q / Esc       | Quit        |

## CLI usage

The game's cli usage is as follows:

```
sharpmines <mode> [options]
```
modes are:

| mode   | board size | mine count |
| ------ | ---------- | ---------- |
| easy   | 9 by 9     | 10         |
| medium | 16 by 16   | 40         |
| hard   | 32 by 16   | 99         |
| custom | arbitrary  | arbitrary  |

The `custom` mode has 3 options, all of type int:

| option | description                |
| ------ | -------------------------- |
| -w     | board width in characters  |
| -h     | board height in characters |
| -c     | mine count                 |
