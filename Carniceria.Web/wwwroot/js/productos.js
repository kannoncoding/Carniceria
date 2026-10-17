(() => {
    "use strict";

    function inicializarEtiquetasProducto() {
        const campoUnidad = document.getElementById("UnidadMedida");
        const etiquetaPrecio = document.getElementById("etiqueta-precio");

        const etiquetaExistenciaActual = document.getElementById(
            "etiqueta-existencia-actual"
        );

        const etiquetaExistenciaMinima = document.getElementById(
            "etiqueta-existencia-minima"
        );

        const ayudaUnidad = document.getElementById(
            "ayuda-unidad-medida"
        );

        if (!campoUnidad ||
            !etiquetaPrecio ||
            !etiquetaExistenciaActual ||
            !etiquetaExistenciaMinima ||
            !ayudaUnidad) {
            return;
        }

        function actualizarEtiquetas() {
            const opcionSeleccionada = campoUnidad.selectedOptions[0];

            if (!opcionSeleccionada || opcionSeleccionada.value === "") {
                etiquetaPrecio.textContent =
                    "Precio por unidad de medida (₡)";

                etiquetaExistenciaActual.textContent =
                    "Existencia actual";

                etiquetaExistenciaMinima.textContent =
                    "Existencia mínima";

                ayudaUnidad.textContent =
                    "Seleccione una unidad de medida. El precio y ambas " +
                    "existencias corresponderán a esa unidad.";

                return;
            }

            // Leer el nombre que el servidor ya incluyó en el desplegable.
            const unidadSingular = opcionSeleccionada.textContent
                .trim()
                .toLowerCase();

            // Formar el plural de las cinco unidades del catálogo actual.
            const unidadPlural = unidadSingular === "unidad"
                ? "unidades"
                : `${unidadSingular}s`;

            etiquetaPrecio.textContent =
                `Precio por ${unidadSingular} (₡)`;

            etiquetaExistenciaActual.textContent =
                `Existencia actual (${unidadPlural})`;

            etiquetaExistenciaMinima.textContent =
                `Existencia mínima (${unidadPlural})`;

            ayudaUnidad.textContent =
                `El precio se expresa en colones por ${unidadSingular}. ` +
                `La existencia actual y la mínima se expresan en ${unidadPlural}.`;
        }

        // Actualizar los textos cuando cambie la unidad seleccionada.
        campoUnidad.addEventListener("change", actualizarEtiquetas);

        // Sincronizar al restaurar la página mediante Atrás/Adelante.
        window.addEventListener("pageshow", actualizarEtiquetas);

        // Usar la selección inicial o la conservada después de un error.
        actualizarEtiquetas();
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            inicializarEtiquetasProducto,
            { once: true }
        );
    } else {
        inicializarEtiquetasProducto();
    }
})();