# Nori

## What is Nori?
A simple, low-fuss, lightweight foundation for building 2D and 3D geometry and engineering applications. The goal is to eventually include all of these:

* 2D & 3D geometry primitives
* Basic trignometry, compuational geometry algorithms
* 2D Entities suitable to read in drawings (points, polygons, text ...)
* 3D Entities suitable to read in models (curves, surfaces ...)
* Kinematics and simulation support
* I/O for basic 2D and 3D filetypes: DXF, GEO, IGS, STEP
* Rendering using an OpenGL engine (with a simple scene-graph interface)

## Design goals
* All code written with modern C#, refactored often to keep technical debt down
* Good test coverage for all code
* Clean layering of core / operations / UI
* High performance
* Elegant, clean, well documented code

## Developer Notes
To try out Nori follow the below steps.

### 1. Clone the repository
To start with, run this command in the terminal from your desired path to clone Nori:  
`git clone https://github.com/tarydon/Nori.git`

### 2. Set up N: drive
Once the repo is cloned, it is compulsory to set up the drive as Nori project files assumes a fixed path from drive `N:`.

1. Substitute the repo: `subst N: <Nori cloned path>`.
>Eg: If you have cloned Nori at _C:/Work_, then `subst N: C:/Work/Nori`

2. Switch to the drive `N:` and ensure the repo files `dir`.

>**TIP:** *This setup process is temporary. To automate this process everytime you restart pc, make a .bat file with the first command and store that file inside your system startup folder.*

### 3. Building and running
- Once the setup is done, open the nori in your IDE and build _Nori.sln_.
- Now you can try executing various projects including running tests and the WPF demo.