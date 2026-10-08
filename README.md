🖥️ Análisis de Lista Circular Simplemente Enlazada (GUI & RAM)
Este proyecto es una aplicación de escritorio desarrollada en C# (Windows Forms). Es una evolución del gestor de listas circulares que incorpora una Interfaz Gráfica de Usuario (GUI), análisis profundo del consumo de memoria (RAM) y mediciones avanzadas procesando inserciones por lotes.

🚀 Características Principales
Interfaz Gráfica: Interfaz visual creada en Windows Forms (Form1) para interactuar con la lista, visualizar datos y accionar las pruebas sin usar la consola.
Medición por Lotes: Sistema de carga de datos fragmentada (MedidorLotes) para entender cómo se degrada o mantiene el rendimiento al insertar miles de registros progresivamente.
Análisis de Memoria (RAM): Incorpora un registro explícito del impacto en memoria al usar la estructura dinámica versus otras aproximaciones. (Para más detalles de resultados, ver el archivo LEEME-ComparacionRAM.md incluido en el repositorio).
Gestión de CSV Extendida: Integración de ComparadorCsv para contrastar distintas fuentes de datos u optimizar su lectura.

📁 Estructura del Proyecto
Form1.cs / Form1.Designer.cs: Código y diseño de la interfaz de usuario interactiva.
CircularListLinked.cs / Node.cs: Implementación core de la lista circular enlazada adaptada para la UI.
ComparadorInserciones.cs: Utilidad de cronometraje de operaciones.
ComparadorCsv.cs: Herramienta para lectura, validación y comparación de lotes de datos estructurados.
MedidorLotes.cs: Lógica específica para realizar pruebas de estrés a la lista insertando registros en bloques (ej. de 100 en 100, 1000 en 1000).

🛠️ Tecnologías y Requisitos
Lenguaje: C#
Entorno: .NET Framework / .NET (Versión Windows)
SO Compatible: Windows (Requerido para la ejecución de aplicaciones WinForms).
IDE Recomendado: Visual Studio.

⚙️ Cómo Ejecutar
Abre el archivo de solución edSimpleLinkedList.sln usando Visual Studio.
Asegúrate de que el proyecto edSimpleLinkedList esté marcado como el "Proyecto de inicio".
Compila y ejecuta la aplicación presionando F5 (o el botón "Iniciar" en Visual Studio).
Usa los botones de la interfaz gráfica para seleccionar tus archivos CSV, cargar los lotes de datos y visualizar el análisis de inserción y RAM.
