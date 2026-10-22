using System.Collections.Generic;

namespace OptiMaster
{
    internal static class ComponentCatalog
    {
        internal static List<ComponentSpec> Create()
        {
            return new List<ComponentSpec>
            {
                new ComponentSpec("DiagTrack", "Telemetría de Windows", "Detiene el envío de diagnósticos en segundo plano.", "Desactiva el envío de informes de uso a Microsoft.", "Bajo", 2, "DiagTrack"),
                new ComponentSpec("WSearch", "Indexación de búsqueda", "Evita lecturas constantes en disco deteniendo el indizador.", "Las búsquedas en el explorador serán más lentas.", "Moderado", 3, "WSearch"),
                new ComponentSpec("SysMain", "SysMain (Superfetch)", "Libera ciclos de lectura y RAM en unidades NVMe/SSD.", "Recomendado desactivar únicamente si usas SSD.", "Moderado", 3, "SysMain"),
                new ComponentSpec("Spooler", "Cola de impresión", "Detiene el servicio de impresoras si no imprimes.", "Las impresoras no funcionarán mientras esté inactivo.", "Bajo", 2, "Spooler"),
                new ComponentSpec("XboxServices", "Servicios de red Xbox", "Apaga demonios de Xbox Live y guardado en nube.", "Afecta a la app Xbox y partidas de Game Pass.", "Bajo", 2, "XblAuthManager", "XblGameSave", "XboxNetApiSvc")
            };
        }
    }
}
