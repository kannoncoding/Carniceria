(() => {
    "use strict";

    // Ajusta etiquetas y ayudas a la unidad seleccionada en el formulario.
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
            // Lee el texto de la opción seleccionada en el formulario.
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

            // Obtiene el nombre visible de la opción renderizada por el servidor.
            const unidadSingular = opcionSeleccionada.textContent
                .trim()
                .toLowerCase();

            // El plural especial cubre la unidad singular del catálogo.
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

        // El cambio de unidad actualiza etiquetas y ayudas.
        campoUnidad.addEventListener("change", actualizarEtiquetas);

        // El evento pageshow restaura las etiquetas al volver a la página.
        window.addEventListener("pageshow", actualizarEtiquetas);

        // Las etiquetas iniciales reflejan la selección del formulario.
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
