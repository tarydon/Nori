# Nori

## What is Nori?
A simple, low-fuss, lightweight foundation for building 2D and 3D geometry and engineering 
applications. The goal is to eventually include all of these:

- 2D & 3D geometry primitives
- Basic trignometry, compuational geometry algorithms
- 2D Entities suitable to read in drawings (points, polygons, text ...)
- 3D Entities suitable to read in models (curves, surfaces ...)
- Kinematics and simulation support
- I/O for basic 2D and 3D filetypes: DXF, GEO, IGS, STEP
- Rendering using an OpenGL engine (with a simple scene-graph interface)

## Design goals
- All code written with modern C#, refactored often to keep technical debt down
- Good test coverage for all code
- Clean layering of core / operations / UI
- High performance
- Elegant, clean, well documented code

## Developer Notes
Set up the environment variable NORIROOT to point to the location where the
NORI repository has been cloned. By default, this maps to N:\ so if you clone
NORI to this location, this environment variable is not required.

Here are the primary projects in the **Nori.sln**:

- **Nori.Core**: The core library for Nori, containing the Nori code (universal
  library, should work on Windows, Mac, Linux).
- **Nori.Gen**: Source generator used by Nori.Core, Nori.WGL to simplify 
  implementation of some patterns (like the Singleton pattern). 
- **Nori.WGL**: The *Lux* rendering engine, built on top of OpenGL (works only
  on Windows).
- **Nori.Test**: Test suite for Nori.
- **Nori.Con**: Console utility for developers (not needed at Nori runtime). 
- **Nori.Doc**: Code documentation tool for Nori (generates HTML documentation 
  pages for Nori code, based on structured XML comments). 
- **Nori.Cover**: Coverage viewer for Nori code (use `Nori.Con coverage` to 
  generate coverage data for this tool to use). 

The **Demos** folder contains a few demo applications for Nori. 