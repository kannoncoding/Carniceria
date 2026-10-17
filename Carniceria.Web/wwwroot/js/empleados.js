(() => {
    "use strict";

    function inicializarPuestosEmpleado() {
        const campoArea = document.getElementById("AreaTrabajo");
        const campoPuesto = document.getElementById("Puesto");
        const elementoCatalogo = document.getElementById(
            "catalogo-puestos-empleado"
        );

        if (!campoArea || !campoPuesto || !elementoCatalogo) {
            return;
        }

        let puestosPorArea;

        try {
            puestosPorArea = JSON.parse(elementoCatalogo.textContent);
        } catch (error) {
            console.error(
                "No se pudo leer el catálogo de puestos del empleado.",
                error
            );

            return;
        }

        function actualizarPuestos() {
            // Conservar la selección únicamente si pertenece al área actual.
            const puestoAnterior = campoPuesto.value;
            const puestosDelArea = puestosPorArea[campoArea.value];

            const puestosPermitidos = Array.isArray(puestosDelArea)
                ? puestosDelArea
                : [];

            const textoInicial = puestosPermitidos.length > 0
                ? "Seleccione un puesto"
                : "Seleccione primero un área de trabajo";

            // Reconstruir las opciones usando texto, sin insertar HTML.
            const opciones = document.createDocumentFragment();

            const opcionInicial = document.createElement("option");
            opcionInicial.value = "";
            opcionInicial.textContent = textoInicial;
            opciones.appendChild(opcionInicial);

            puestosPermitidos.forEach((puesto) => {
                const opcion = document.createElement("option");
                opcion.value = puesto;
                opcion.textContent = puesto;
                opciones.appendChild(opcion);
            });

            campoPuesto.replaceChildren(opciones);

            campoPuesto.value = puestosPermitidos.includes(puestoAnterior)
                ? puestoAnterior
                : "";

            campoPuesto.disabled = puestosPermitidos.length === 0;
        }

        // Actualizar las opciones cuando el usuario cambie de área.
        campoArea.addEventListener("change", actualizarPuestos);

        // Sincronizar también al volver mediante Atrás/Adelante.
        window.addEventListener("pageshow", actualizarPuestos);

        // Respetar una selección válida renderizada por el servidor.
        actualizarPuestos();
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            inicializarPuestosEmpleado,
            { once: true }
        );
    } else {
        inicializarPuestosEmpleado();
    }
})();