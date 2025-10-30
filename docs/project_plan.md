# ANT Mission Manager - Project Plan

## Overview
WPF-based ANT Mission Manager application for managing autonomous vehicle missions, routes, and real-time monitoring.

## Recent Changes

### 2025-10-30: Map View Implementation

#### Added Components
1. **Map Data Models** (Models/MapData.cs)
   - `MapData`: Main map container with layers
   - `MapLayer`: Layer containing nodes and links
   - `MapNode`: Node representation with X, Y, Z coordinates
   - `MapLink`: Link between nodes with start/end coordinates

2. **API Service Extension** (Services/AntApiService.cs)
   - `GetMapDataAsync()`: Fetches complete map data including nodes and links from `/maps/level/1/data` endpoint

3. **Map View UserControl** (Views/MapView.xaml + MapView.xaml.cs)
   - Canvas-based rendering for optimal performance
   - Features:
     - Renders navigation nodes as circles
     - Renders navigation links as blue lines
     - Displays vehicle positions (only insert state)
     - Shows mission paths as red lines
     - Interactive zoom/pan controls
     - Mouse wheel for zoom in/out
     - Mouse drag for pan (horizontal) or zoom (vertical)
     - Reset view button
   - Real-time zoom and offset indicators

4. **MainWindow Integration**
   - Added "Map View" tab to TabControl
   - Binds MapData and Vehicles to MapView control

5. **ViewModel Integration** (ViewModels/MainViewModel.cs)
   - Added `MapData` property
   - Added `ExecuteRefreshMap()` method
   - Integrated map loading into `LoadInitialServerData()`

#### Features Implemented
- **Node Rendering**: Circular nodes with labels
- **Link Rendering**: Lines connecting nodes
- **Vehicle Display**:
  - Only shown in insert state (operatingstate != 0)
  - Hidden in extract state
  - Displayed as green circles with yellow borders
- **Mission Path Rendering**: Red lines showing active vehicle paths
- **Interactive Controls**:
  - Mouse wheel: Zoom in/out
  - Mouse drag vertical: Zoom control (up=zoom out, down=zoom in)
  - Mouse drag horizontal: Pan control
  - Reset view button
- **Auto-fit**: Map automatically scales to fit canvas
- **Real-time Updates**: Map refreshes with vehicle position updates

## Application Structure

### Views
- MainWindow: Main application window with tabs
- LoginWindow: User authentication
- MapView: Real-time map visualization (NEW)
- Common dialogs

### ViewModels
- MainViewModel: Main application logic
- LoginViewModel: Authentication logic

### Models
- MissionInfo: Mission details
- Vehicle: Vehicle state and position
- NodeInfo: Node information
- MapData/MapLayer/MapNode/MapLink: Map structure (NEW)
- AlarmInfo: System alarms

### Services
- AntApiService: REST API communication
- FileService: File I/O operations

## Current Tabs
1. Mission Dashboard: Overview and mission management
2. Mission Router: Route configuration
3. Vehicle Management: Vehicle control
4. Alarm Log: System alarms
5. **Map View**: Real-time map visualization (NEW)

## Technical Stack
- Framework: .NET 8.0 WPF
- UI Library: MaterialDesignInXAML
- HTTP Client: HttpClient
- JSON: Newtonsoft.Json

## API Endpoints Used
- `/login`: Authentication
- `/vehicles`: Vehicle information
- `/missions`: Mission management
- `/maps/level/1/data`: Map data (nodes, links, layers) (NEW)
- `/alarms`: System alarms

## Future Enhancements
- Node selection and inspection
- Vehicle path prediction
- Map layers toggle
- Custom map overlays
- Export map as image
