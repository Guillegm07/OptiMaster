# OptiGuille

Aplicación de escritorio para Windows 11, en español. Ejecuta `OptiGuille.exe`; Windows pedirá permisos de administrador para las acciones del sistema. La interfaz no requiere abrir PowerShell. Está construida con C# y Windows Forms.

## Interfaz

- Ventana única redimensionable con barra de título y controles nativos de Windows.
- Menú lateral con seis vistas: Limpiador de Basura, Acelerador de Inicio, Liberador de RAM, Seguridad, Desfragmentar y Ajustes.
- Las lecturas de CPU, RAM y almacenamiento se actualizan mientras la aplicación está abierta.

El porcentaje del anillo es un indicador visual calculado a partir de las lecturas de CPU, RAM y espacio usado. No mide una optimización real ni predice FPS o latencia.

## Funciones

- Limpieza de temporales del usuario y de Windows, Papelera y, opcionalmente, cachés de sombreadores o de Chrome, Edge y Firefox. No borra cookies ni sesiones.
- Inicio de aplicaciones a través de Configuración de Windows y consulta de tareas programadas.
- Lista de procesos ordenada por memoria, ajuste de prioridad y afinidad de CPU con opción de restaurar los valores originales, y finalización manual de procesos.
- Modo Juego, Xbox Game DVR y plan de energía de alto rendimiento, con copias de los ajustes previos para restaurarlos.
- Interruptores para servicios opcionales con descripción, estimación de impacto y restauración: diagnóstico, búsqueda, impresión, Xbox y mapas.
- Actualización de aplicaciones compatibles con winget, copia de controladores con PnPUtil, información SMART cuando el dispositivo la expone y accesos a Seguridad de Windows y Windows Update.
- Optimización de unidades mediante la herramienta de Windows, que selecciona TRIM para SSD o desfragmentación para HDD; búsqueda SHA-256 de duplicados con revisión antes de enviarlos a la Papelera.
- Reparación de Windows mediante SFC o DISM, restablecimiento de Microsoft Store, reinicio de la cola de impresión, borrado de archivos con tres pasadas y acceso a Windows File Recovery si está instalado.

Las franjas de impacto de servicios son estimaciones relativas, no mediciones de ahorro ni promesas de FPS. La aplicación no fuerza el vaciado de la memoria en espera, no limpia el registro a ciegas, no cambia parámetros TCP genéricos, no instala controladores de terceros ni suspende automáticamente procesos de otras aplicaciones. Windows no ofrece un retraso seguro para todo tipo de inicio, por lo que la gestión se deriva a su configuración.

## Archivos

- `OptiGuille.exe`: aplicación ejecutable.
- `OptiGuille.cs` y `UiComponents.cs`: código fuente.
- `OptiGuille.manifest`: manifiesto de la aplicación.
