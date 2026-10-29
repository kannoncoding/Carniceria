(() => {
    "use strict";

    // Actualiza las fechas visibles con el formato usado por el sitio.
    function inicializarResumenesFechas() {
        // Busca campos vinculados a un resumen de fecha.
        const camposFecha = document.querySelectorAll(
            'input[type="date"][data-fecha-resumen]'
        );

        camposFecha.forEach((campo) => {
            const resumenId = campo.dataset.fechaResumen;

            if (!resumenId) {
                return;
            }

            const resumen = document.getElementById(resumenId);

            if (!resumen) {
                return;
            }

            function actualizarResumen() {
                // Divide el valor ISO en año, mes y día.
                const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(
                    campo.value
                );

                if (!partes) {
                    resumen.textContent =
                        "Fecha seleccionada: sin seleccionar.";

                    return;
                }

                const anio = partes[1];
                const mes = partes[2];
                const dia = partes[3];

                // Presenta la fecha sin conversión de zona horaria.
                resumen.textContent =
                    `Fecha seleccionada: ${dia}/${mes}/${anio}.`;
            }

            campo.addEventListener("input", actualizarResumen);
            campo.addEventListener("change", actualizarResumen);

            // El evento pageshow sincroniza valores restaurados por el navegador.
            window.addEventListener("pageshow", actualizarResumen);

            actualizarResumen();
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            inicializarResumenesFechas,
            { once: true }
        );
    } else {
        inicializarResumenesFechas();
    }
})();
