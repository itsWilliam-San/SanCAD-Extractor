using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sewarage.Models;

namespace Sewarage.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public partial class FindPluginViewModel : ViewModelBase
{
    public List<Plugin> Plugins { get; } = new List<Plugin>()
    {
        new Plugin()
        {
           Author = "CivilSystem Inc", 
           Description = "Exports the defined gravity and pressurized network layers into georeferenced GIS Shapefiles (.shp). Preserves hydraulic coefficients and local coordinate projections.",
           Name = "GIS Shapefile Exporter",
           Version = "1.2.0"
        },
        
        new Plugin()
    {
        Author = "HydroTech Solutions",
        Description = "Automates the extraction of gravity sewer lines, manholes, and invert levels directly from AutoCAD DWG layer standards.",
        Name = "AutoCAD Network Extractor",
        Version = "2.1.0"
    },
    new Plugin()
    {
        Author = "BIM Flow Studio",
        Description = "Reads Revit MEP sanitary pipe families and structures, converting them into the core format for hydraulic analysis.",
        Name = "Revit BIM Importer",
        Version = "1.0.4"
    },
    new Plugin()
    {
        Author = "CivilSystem Inc",
        Description = "Exports the defined gravity and pressurized network layers into georeferenced GIS Shapefiles (.shp). Preserves hydraulic coefficients and local coordinate projections.",
        Name = "GIS Shapefile Exporter",
        Version = "1.2.0"
    },
    new Plugin()
    {
        Author = "Sanitation Corp",
        Description = "Performs automated validation checks on the sewer network, flagging backward slopes, missing invert levels, and disconnected manholes.",
        Name = "Network Validator & Error Checker",
        Version = "3.0.1"
    },
    new Plugin()
    {
        Author = "Apex Engineering Tools",
        Description = "Generates detailed longitudinal profile sheets (perfil altimétrico) split automatically between manholes for CAD production.",
        Name = "Longitudinal Profile Generator",
        Version = "1.5.2"
    },
    new Plugin()
    {
        Author = "DataMorphix",
        Description = "Compiles all network nodes, pipe diameters, slopes, and flow rates into a standardized Excel spreadsheet following municipal guidelines.",
        Name = "Excel Hydraulic Report",
        Version = "2.0.0"
    },
    new Plugin()
    {
        Author = "ZWCAD Plugins Ltda",
        Description = "Specialized connector plugin designed to extract text attributes, invert levels, and street names natively from ZWCAD drawings.",
        Name = "ZWCAD Attribute Reader",
        Version = "1.1.0"
    },
    new Plugin()
    {
        Author = "EcoFlow Dynamics",
        Description = "Simulates peak flow capacity using Manning's equation across all selected segments, providing safety margins and velocity warnings.",
        Name = "Manning Flow Analyzer",
        Version = "1.3.5"
    },
    new Plugin()
    {
        Author = "DocuPrint Solutions",
        Description = "Bundles all generated sections, layout sheets, and material summary tables into a single professional PDF report package.",
        Name = "PDF Project Packager",
        Version = "2.2.1"
    },
    new Plugin()
    {
        Author = "OpenBridge Dev",
        Description = "Experimental plugin enabling experimental data exchange and file reading compatibility with FreeCAD and open-source BIM models.",
        Name = "FreeCAD Data Bridge",
        Version = "0.8.2"
    }
    };
}