// DTOs that match the backend API

export interface ProductoDTO {
  id: string;
  codigoBarras: string;
  nombre: string;
  descripcion?: string;
  categoriaId: number;
  precioCosto: number;
  precioVenta: number;
  activo: boolean;
  fechaCreacion: string;
  // Tax Engine
  impuestoId?: number;
  nombreImpuesto?: string;       // "IVA 19%"
  tipoImpuesto?: string;         // "IVA" | "INC" | "Saludable" | "Bolsa"
  porcentajeImpuesto?: number;   // 0.19 — usar en estimación del carrito
  esAlimentoUltraprocesado: boolean;
  gramosAzucarPor100ml?: number;
  unidadMedida: string;          // Código DIAN: "94"=Unidad, "KGM"=Kg, etc.
  // Concepto Retención DIAN
  conceptoRetencionId?: number;
  conceptoRetencionNombre?: string;
  // Lotes
  manejaLotes: boolean;
  diasVidaUtil?: number;
}

export interface CrearProductoDTO {
  codigoBarras: string;
  nombre: string;
  descripcion?: string;
  categoriaId: number;
  precioCosto: number;
  precioVenta: number;
  impuestoId?: number;
  esAlimentoUltraprocesado?: boolean;
  gramosAzucarPor100ml?: number;
  unidadMedida?: string;
  conceptoRetencionId?: number;
  manejaLotes?: boolean;
  diasVidaUtil?: number;
}

export interface ActualizarProductoDTO {
  nombre: string;
  descripcion?: string;
  precioCosto: number;
  precioVenta: number;
  impuestoId?: number;
  esAlimentoUltraprocesado?: boolean;
  gramosAzucarPor100ml?: number;
  unidadMedida?: string;
  conceptoRetencionId?: number;
  manejaLotes?: boolean;
  diasVidaUtil?: number;
}

export interface CategoriaDTO {
  id: number;
  nombre: string;
  descripcion?: string;
  activa: boolean;
  categoriaPadreId?: number;
  nombrePadre?: string;
  nivel: number;
  rutaCompleta: string;
  cantidadSubCategorias: number;
  cantidadProductos: number;
  margenGanancia: number;
  cuentaInventario?: string;
  cuentaCosto?: string;
  cuentaIngreso?: string;
  externalId?: string;
  origenDatos: string;
}

export interface CategoriaArbolDTO {
  id: number;
  nombre: string;
  descripcion?: string;
  activa: boolean;
  categoriaPadreId?: number;
  nivel: number;
  rutaCompleta: string;
  cantidadProductos: number;
  margenGanancia: number;
  subCategorias: CategoriaArbolDTO[];
}

export interface CrearCategoriaDTO {
  nombre: string;
  descripcion?: string;
  categoriaPadreId?: number;
  margenGanancia?: number;
  cuentaInventario?: string;
  cuentaCosto?: string;
  cuentaIngreso?: string;
  externalId?: string;
  origenDatos?: string;
}

export interface ActualizarCategoriaDTO {
  nombre: string;
  descripcion?: string;
  categoriaPadreId?: number;
  margenGanancia?: number;
  cuentaInventario?: string;
  cuentaCosto?: string;
  cuentaIngreso?: string;
  externalId?: string;
  origenDatos?: string;
}

export interface MoverCategoriaDTO {
  categoriaId: number;
  nuevaCategoriaPadreId?: number;
}

export interface PrecioSucursalDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  sucursalId: number;
  nombreSucursal: string;
  precioVenta: number;
  precioMinimo?: number;
  fechaModificacion?: string;
}

export interface CrearPrecioSucursalDTO {
  productoId: string;
  sucursalId: number;
  precioVenta: number;
  precioMinimo?: number;
  origenDato?: string; // "Manual", "Migrado", "Importado"
}

export interface PrecioResueltoDTO {
  precioVenta: number;
  precioMinimo?: number;
  origen: string; // "Sucursal", "Producto", "Margen"
  origenDato?: string; // "Manual", "Migrado" (si origen es "Sucursal")
}

export interface PrecioResueltoLoteItemDTO {
  productoId: string;
  precioVenta: number;
  precioMinimo?: number;
  origen: string; // "Sucursal", "Producto", "Margen"
}

export interface StockDTO {
  productoId: string;
  productoNombre: string;
  productosCodigo: string;
  sucursalId: string;
  sucursalNombre: string;
  cantidad: number;
  stockMinimo: number;
  costoPromedio: number;
}

export interface VentaDTO {
  id: number;
  numeroVenta: string;
  sucursalId: number;
  nombreSucursal: string;
  cajaId: number;
  nombreCaja: string;
  clienteId?: number;
  nombreCliente?: string;
  subtotal: number;
  descuento: number;
  impuestos: number;
  total: number;
  estado: string;
  metodoPago: string;
  montoPagado?: number;
  cambio?: number;
  observaciones?: string;
  fechaVenta: string;
  requiereFacturaElectronica: boolean;
  detalles: LineaVentaDTO[];
  // ERP Sync
  sincronizadoErp: boolean;
  fechaSincronizacionErp?: string;
  erpReferencia?: string;
  errorSincronizacion?: string;
}

export interface DetalleVentaLoteDTO {
  loteInventarioId: number;
  numeroLote?: string;
  cantidad: number;
  costoUnitario: number;
}

export interface LineaVentaDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  numeroLote?: string;
  cantidad: number;
  precioUnitario: number;
  costoUnitario: number;
  descuento: number;
  porcentajeImpuesto: number;
  montoImpuesto: number;
  subtotal: number;
  margenGanancia: number;
  lotes: DetalleVentaLoteDTO[];
}

export interface CrearVentaDTO {
  sucursalId: number;
  cajaId: number;
  clienteId?: number;
  metodoPago: number; // 0=Efectivo, 1=Tarjeta, 2=Transferencia
  montoPagado?: number;
  observaciones?: string;
  lineas: CrearLineaVentaDTO[];
  fechaVenta?: string; // ISO 8601 UTC — null usa DateTime.UtcNow en el servidor
}

export interface CrearLineaVentaDTO {
  productoId: string;
  cantidad: number;
  precioUnitario?: number; // null = usa precio resuelto automáticamente
  descuento: number; // Valor absoluto en pesos, no porcentaje
}

export interface CajaDTO {
  id: number;
  nombre: string;
  sucursalId: number;
  nombreSucursal?: string;
  estado: string; // 'Abierta' | 'Cerrada'
  montoApertura: number;
  montoActual: number;
  fechaApertura?: string;
  fechaCierre?: string;
  activa: boolean;
}

export interface CrearCajaDTO {
  nombre: string;
  sucursalId: number;
}

export interface AbrirCajaDTO {
  montoApertura: number;
}

export interface CerrarCajaDTO {
  montoCierre: number;
  observaciones?: string;
}

// ============================================
// ÓRDENES DE COMPRA
// ============================================

export interface OrdenCompraDTO {
  id: number;
  numeroOrden: string;
  sucursalId: number;
  nombreSucursal: string;
  proveedorId: number;
  nombreProveedor: string;
  estado: string; // 'Pendiente' | 'Aprobada' | 'RecibidaParcial' | 'RecibidaCompleta' | 'Rechazada' | 'Cancelada' | 'DevueltaTotalmente'
  formaPago: string; // 'Contado' | 'Credito'
  diasPlazo: number;
  fechaOrden: string;
  fechaEntregaEsperada?: string;
  fechaAprobacion?: string;
  fechaRecepcion?: string;
  aprobadoPor?: string;
  recibidoPor?: string;
  observaciones?: string;
  motivoRechazo?: string;
  subtotal: number;
  impuestos: number;
  total: number;
  requiereFacturaElectronica: boolean;
  sincronizadoErp?: boolean;
  fechaSincronizacionErp?: string;
  erpReferencia?: string;
  errorSincronizacion?: string;
  detalles: DetalleOrdenCompraDTO[];
}

export interface DetalleOrdenCompraDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  cantidadSolicitada: number;
  cantidadRecibida: number;
  precioUnitario: number;
  porcentajeImpuesto: number;
  montoImpuesto: number;
  subtotal: number;
  nombreImpuesto?: string;
  observaciones?: string;
  manejaLotes: boolean;
  diasVidaUtil?: number;
}

export interface CrearOrdenCompraDTO {
  sucursalId: number;
  proveedorId: number;
  fechaEntregaEsperada?: string;
  formaPago: string;
  diasPlazo: number;
  observaciones?: string;
  lineas: LineaOrdenCompraDTO[];
  fechaOrden?: string;
}

export interface LineaOrdenCompraDTO {
  productoId: string;
  cantidad: number;
  precioUnitario: number;
  impuestoId?: number;
}

export interface ActualizarOrdenCompraDTO {
  fechaEntregaEsperada?: string;
  observaciones?: string;
  formaPago?: string;
  diasPlazo?: number;
  lineas?: LineaOrdenCompraDTO[];
}

export interface RecibirOrdenCompraDTO {
  lineas: LineaRecepcionOrdenCompraDTO[];
  fechaRecepcion?: string; // ISO 8601 UTC — null usa DateTime.UtcNow en el servidor
}

export interface LineaRecepcionOrdenCompraDTO {
  productoId: string;
  cantidadRecibida: number;
  observaciones?: string;
  numeroLote?: string;
  fechaVencimiento?: string; // 'YYYY-MM-DD'
}

export interface AprobarOrdenCompraDTO {
  observaciones?: string;
}

export interface RechazarOrdenCompraDTO {
  motivoRechazo: string;
}

export interface CancelarOrdenCompraDTO {
  motivo: string;
}

export interface CrearDevolucionCompraDTO {
  motivo: string;
  lineas: LineaDevolucionCompraDTO[];
}

export interface LineaDevolucionCompraDTO {
  productoId: string;
  cantidad: number;
}

export interface DevolucionCompraDTO {
  id: number;
  ordenCompraId: number;
  numeroOrden: string;
  numeroDevolucion: string;
  motivo: string;
  total: number;
  fechaDevolucion: string;
  autorizadoPor?: string;
  detalles: DetalleDevolucionCompraDTO[];
}

export interface DetalleDevolucionCompraDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  cantidadDevuelta: number;
  precioUnitario: number;
  subtotal: number;
}

export interface ErpOutboxErrorDTO {
  id: number;
  tipoDocumento: string;
  entidadId: number;
  fechaCreacion: string;
  fechaProcesamiento?: string;
  intentos: number;
  ultimoError?: string;
  estado: string;
}

export interface SucursalDTO {
  id: number;
  nombre: string;
  direccion?: string;
  codigoPais?: string;
  nombrePais?: string;
  ciudad?: string;
  telefono?: string;
  email?: string;
  centroCosto?: string;
  metodoCosteo: string;
  activa: boolean;
  fechaCreacion?: string;
  empresaId: number;
}

export interface CrearSucursalDTO {
  nombre: string;
  direccion?: string;
  codigoPais?: string;
  nombrePais?: string;
  ciudad?: string;
  telefono?: string;
  email?: string;
  centroCosto?: string;
  metodoCosteo?: string;
}

export interface ActualizarSucursalDTO {
  nombre: string;
  direccion?: string;
  codigoPais?: string;
  nombrePais?: string;
  ciudad?: string;
  telefono?: string;
  email?: string;
  centroCosto?: string;
  metodoCosteo?: string;
}

export interface PaisDTO {
  iso2: string;
  nombre: string;
  nombreNativo?: string;
  emoji?: string;
}

export interface CiudadDTO {
  nombre: string;
  codigoPais?: string;
  latitud?: number;
  longitud?: number;
}

export interface TerceroActividadDTO {
  id: number;
  codigoCIIU: string;
  descripcion: string;
  esPrincipal: boolean;
}

export interface TerceroDTO {
  id: number;
  tipoIdentificacion: string;
  identificacion: string;
  digitoVerificacion?: string;
  nombre: string;
  tipoTercero: string;
  telefono?: string;
  email?: string;
  direccion?: string;
  ciudad?: string;
  codigoDepartamento?: string;
  codigoMunicipio?: string;
  perfilTributario: string;
  esGranContribuyente: boolean;
  esAutorretenedor: boolean;
  esResponsableIVA: boolean;
  origenDatos: string;
  externalId?: string;
  activo: boolean;
  actividades: TerceroActividadDTO[];
}

export interface CrearTerceroDTO {
  tipoIdentificacion: string;
  identificacion: string;
  nombre: string;
  tipoTercero: string;
  telefono?: string;
  email?: string;
  direccion?: string;
  ciudad?: string;
  codigoDepartamento?: string;
  codigoMunicipio?: string;
  perfilTributario?: string;
  esGranContribuyente?: boolean;
  esAutorretenedor?: boolean;
  esResponsableIVA?: boolean;
}

export interface ActualizarTerceroDTO {
  nombre: string;
  tipoTercero?: string;
  telefono?: string;
  email?: string;
  direccion?: string;
  ciudad?: string;
  codigoDepartamento?: string;
  codigoMunicipio?: string;
  perfilTributario?: string;
  esGranContribuyente?: boolean;
  esAutorretenedor?: boolean;
  esResponsableIVA?: boolean;
}

export interface AgregarActividadDTO {
  codigoCIIU: string;
  descripcion: string;
  esPrincipal?: boolean;
}

export interface ResultadoFilaTerceroDTO {
  fila: number;
  identificacion?: string;
  nombre?: string;
  estado: 'Importado' | 'Omitido' | 'Error';
  mensaje?: string;
}

export interface ResultadoImportacionTercerosDTO {
  totalFilas: number;
  importados: number;
  omitidos: number;
  errores: number;
  filas: ResultadoFilaTerceroDTO[];
}

export interface SucursalResumenDTO {
  id: number;
  nombre: string;
  empresaId: number;
  empresaNombre?: string;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  nombre: string;
  roles: string[];
  rol?: string;
  sucursalId?: number;
  sucursalNombre?: string;
  sucursalesDisponibles: SucursalResumenDTO[];
  empresaId?: number;
  empresaNombre?: string;
  /** Lista explícita de empresas disponibles (viene del backend, incluye empresas sin sucursales) */
  empresasDisponibles?: { id: number; nombre: string }[];
}

// ============================================
// REPORTES
// ============================================

export interface ReporteVentasDTO {
  totalVentas: number;
  cantidadVentas: number;
  ticketPromedio: number;
  costoTotal: number;
  utilidadTotal: number;
  margenPromedio: number;
  ventasPorMetodoPago: VentaPorMetodoPagoDTO[];
  ventasPorDia: VentaPorDiaDTO[];
}

export interface VentaPorMetodoPagoDTO {
  metodo: string;
  total: number;
  cantidad: number;
}

export interface VentaPorDiaDTO {
  fecha: string;
  total: number;
  cantidad: number;
  costoTotal: number;
  utilidad: number;
}

export interface ReporteInventarioValorizadoDTO {
  totalCosto: number;
  totalVenta: number;
  utilidadPotencial: number;
  totalProductos: number;
  totalUnidades: number;
  productos: ProductoValorizadoDTO[];
}

export interface ProductoValorizadoDTO {
  productoId: string;
  codigoBarras: string;
  nombre: string;
  categoria?: string;
  sucursalId: number;
  nombreSucursal: string;
  cantidad: number;
  costoPromedio: number;
  costoTotal: number;
  precioVenta: number;
  valorVenta: number;
  utilidadPotencial: number;
  margenPorcentaje: number;
}

export interface ReporteCajaDTO {
  cajaId: number;
  nombreCaja: string;
  sucursalId: number;
  nombreSucursal: string;
  fechaApertura: string;
  fechaCierre?: string;
  montoApertura: number;
  totalVentasEfectivo: number;
  totalVentasTarjeta: number;
  totalVentasTransferencia: number;
  totalVentas: number;
  montoCierre?: number;
  diferenciaEsperado?: number;
  diferenciaReal?: number;
  ventas: VentaCajaDTO[];
}

export interface VentaCajaDTO {
  ventaId: number;
  numeroVenta: string;
  fechaVenta: string;
  metodoPago: string;
  total: number;
  costoTotal: number;
  utilidad: number;
  cliente?: string;
}

// ─── Dashboard ─────────────────────────────────────────────

export interface DashboardDTO {
  metricasDelDia: MetricasDelDiaDTO;
  ventasPorHora: VentaPorHoraDTO[];
  topProductos: TopProductoDTO[];
  alertasStock: AlertaStockDTO[];
}

export interface MetricasDelDiaDTO {
  ventasTotales: number;
  ventasAyer: number;
  porcentajeCambio: number;
  cantidadVentas: number;
  productosVendidos: number;
  clientesAtendidos: number;
  ticketPromedio: number;
  utilidadDelDia: number;
  margenPromedio: number;
}

export interface VentaPorHoraDTO {
  hora: number;
  total: number;
  cantidad: number;
}

export interface TopProductoDTO {
  productoId: string;
  codigoBarras: string;
  nombre: string;
  categoria: string | null;
  cantidadVendida: number;
  totalVentas: number;
  utilidad: number;
  margenPorcentaje: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ImpuestoDTO {
  id: number;
  nombre: string;
  tipo: string;          // "IVA" | "INC" | "Saludable" | "Bolsa"
  porcentaje: number;    // 0.19 para 19%
  valorFijo?: number;    // valor fijo/unidad (bolsa)
  codigoCuentaContable?: string;
  aplicaSobreBase: boolean;
  codigoPais: string;
  descripcion?: string;
}

export interface CrearImpuestoDTO {
  nombre: string;
  tipo: string;
  porcentaje: number;
  valorFijo?: number;
  codigoCuentaContable?: string;
  aplicaSobreBase?: boolean;
  codigoPais?: string;
  descripcion?: string;
}

export interface EditarImpuestoDTO {
  nombre?: string;
  porcentaje?: number;
  valorFijo?: number;
  codigoCuentaContable?: string;
  descripcion?: string;
}

export interface RetencionReglaDTO {
  id: number;
  nombre: string;
  tipo: string;           // "ReteFuente" | "ReteICA" | "ReteIVA"
  porcentaje: number;
  baseMinUVT: number;
  codigoMunicipio?: string;
  perfilVendedor: string;
  perfilComprador: string;
  codigoCuentaContable?: string;
  activo: boolean;
  conceptoRetencionId?: number;
  conceptoRetencionNombre?: string;
}

export interface CrearRetencionDTO {
  nombre: string;
  tipo: string;
  porcentaje: number;
  baseMinUVT: number;
  codigoMunicipio?: string;
  perfilVendedor: string;
  perfilComprador: string;
  codigoCuentaContable?: string;
  conceptoRetencionId?: number;
}

export interface ConceptoRetencionDTO {
  id: number;
  nombre: string;
  codigoDian?: string;
  porcentajeSugerido?: number;
  activo: boolean;
}

export interface CrearConceptoRetencionDTO {
  nombre: string;
  codigoDian?: string;
  porcentajeSugerido?: number;
}

export interface EditarConceptoRetencionDTO {
  nombre?: string;
  codigoDian?: string;
  porcentajeSugerido?: number;
}

export interface ApiError {
  message: string;
  error?: string;
  title?: string;
  errors?: Record<string, string[]>;
  statusCode: number;
  response?: { status: number; data: any };
  request?: unknown;
}

// ============================================
// DEVOLUCIONES
// ============================================

export interface DevolucionVentaDTO {
  id: number;
  ventaId: number;
  numeroVenta: string;
  numeroDevolucion: string;
  motivo: string;
  totalDevuelto: number;
  fechaDevolucion: string;
  autorizadoPor?: string;
  detalles: DetalleDevolucionDTO[];
}

export interface DetalleDevolucionDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  cantidadDevuelta: number;
  precioUnitario: number;
  costoUnitario: number;
  subtotalDevuelto: number;
}

export interface CrearDevolucionParcialDTO {
  motivo: string;
  lineas: LineaDevolucionDTO[];
}

export interface LineaDevolucionDTO {
  productoId: string;
  cantidad: number;
}

// ============================================
// INVENTARIO
// ============================================

export interface InventarioStockDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  codigoBarras?: string;
  sucursalId: number;
  nombreSucursal: string;
  cantidad: number;
  stockMinimo: number;
  costoPromedio: number;
  ultimaActualizacion: string;
}

export interface AlertaStockDTO {
  productoId: string;
  nombreProducto: string;
  codigoBarras?: string;
  sucursalId: number;
  nombreSucursal: string;
  cantidadActual: number;
  stockMinimo: number;
}

export interface MovimientoInventarioDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  sucursalId: number;
  nombreSucursal: string;
  tipoMovimiento: string;
  cantidad: number;
  costoUnitario: number;
  costoTotal: number;
  porcentajeImpuesto: number;
  montoImpuesto: number;
  referencia?: string;
  observaciones?: string;
  terceroId?: number;
  nombreTercero?: string;
  fechaMovimiento: string;
}

export interface EntradaInventarioDTO {
  productoId: string;
  sucursalId: number;
  cantidad: number;
  costoUnitario: number;
  porcentajeImpuesto: number;
  terceroId?: number;
  referencia?: string;
  observaciones?: string;
  fechaMovimiento?: string; // ISO 8601 UTC — null usa DateTime.UtcNow en el servidor
}

export interface AjusteInventarioDTO {
  productoId: string;
  sucursalId: number;
  cantidadNueva: number;
  observaciones?: string;
}

export interface DevolucionProveedorDTO {
  productoId: string;
  sucursalId: number;
  cantidad: number;
  terceroId: number;
  referencia?: string;
  observaciones?: string;
}
// ─── Lotes ─────────────────────────────────────────────

export interface LoteDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  codigoBarras?: string;
  sucursalId: number;
  nombreSucursal: string;
  numeroLote?: string;
  fechaVencimiento?: string; // 'YYYY-MM-DD'
  ordenCompraId?: number;
  cantidadInicial: number;
  cantidadDisponible: number;
  costoUnitario: number;
  referencia?: string;
  fechaEntrada: string;
}

export interface AlertaLoteDTO {
  loteId: number;
  productoId: string;
  nombreProducto: string;
  codigoBarras?: string;
  sucursalId: number;
  nombreSucursal: string;
  numeroLote?: string;
  fechaVencimiento: string; // 'YYYY-MM-DD'
  diasParaVencer: number;
  cantidadDisponible: number;
  fechaEntrada: string;     // ISO timestamp de la entrada al inventario
}

export interface ActualizarLoteDTO {
  numeroLote?: string;
  fechaVencimiento?: string; // 'YYYY-MM-DD'
}

export interface TrazabilidadEntradaDTO {
  tipo: string;
  referencia: string;
  fecha: string;
  proveedor?: string;
  cantidadInicial: number;
  costoUnitario: number;
}

export interface TrazabilidadMovimientoDTO {
  tipo: string;
  referencia: string;
  fecha: string;
  cantidad: number;
  detalle?: string;
  saldo: number;
}

export interface TrazabilidadLoteDTO {
  lote: LoteDTO;
  entrada?: TrazabilidadEntradaDTO;
  movimientos: TrazabilidadMovimientoDTO[];
}

// ─── Reporte de Lotes por Vencimiento ──────────────────────────────────────

export interface LoteReporteItemDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  codigoBarras?: string;
  sucursalId: number;
  nombreSucursal: string;
  numeroLote?: string;
  fechaVencimiento?: string;   // 'YYYY-MM-DD'
  diasParaVencer?: number;     // null = sin fecha de vencimiento
  cantidadDisponible: number;
  costoUnitario: number;
  valorTotal: number;
  referencia?: string;
  fechaEntrada: string;
  estadoVencimiento: 'Vencido' | 'Critico' | 'Proximo' | 'Vigente' | 'SinFecha';
}

export interface ReporteLotesDTO {
  totalLotes: number;
  totalUnidades: number;
  valorTotalInventario: number;
  lotesVencidos: number;
  lotesCriticos: number;
  lotesProximos: number;
  lotesVigentes: number;
  lotesSinFecha: number;
  items: LoteReporteItemDTO[];
}

// ─── Traslados ─────────────────────────────────────────

export interface TrasladoDTO {
  id: number;
  numeroTraslado: string;
  sucursalOrigenId: number;
  nombreSucursalOrigen: string;
  sucursalDestinoId: number;
  nombreSucursalDestino: string;
  estado: string; // 'Pendiente' | 'EnTransito' | 'Recibido' | 'Rechazado' | 'Cancelado'
  fechaTraslado: string;
  fechaEnvio?: string;
  fechaRecepcion?: string;
  observaciones?: string;
  detalles: DetalleTrasladoDTO[];
}

export interface DetalleTrasladoDTO {
  id: number;
  productoId: string;
  nombreProducto: string;
  codigoBarras: string;
  cantidadSolicitada: number;
  cantidadRecibida: number;
  costoUnitario: number;
}

export interface CrearTrasladoDTO {
  sucursalOrigenId: number;
  sucursalDestinoId: number;
  observaciones?: string;
  lineas: LineaTrasladoDTO[];
  fechaTraslado?: string;
}

export interface LineaTrasladoDTO {
  productoId: string;
  cantidad: number;
}

export interface RecibirTrasladoDTO {
  lineas: LineaRecepcionDTO[];
  observaciones?: string | null;
}

export interface LineaRecepcionDTO {
  productoId: string;
  cantidadRecibida: number;
  observaciones?: string | null;
}

export interface RechazarTrasladoDTO {
  motivo: string;
}

export interface CancelarTrasladoDTO {
  motivo: string;
}

// ── Auditoría ──────────────────────────────────────────────────────────────

export interface ActivityLogFullDTO {
  id: number;
  usuarioEmail: string;
  usuarioNombre?: string;
  usuarioId?: number;
  fechaHora: string;
  accion: string;
  tipo: number;
  tipoNombre: string;
  sucursalId?: number;
  nombreSucursal?: string;
  ipAddress?: string | null;
  userAgent?: string;
  tipoEntidad?: string;
  entidadId?: string;
  entidadNombre?: string;
  descripcion?: string | null;
  datosAnteriores?: string | null;
  datosNuevos?: string | null;
  metadatos?: string;
  exitosa: boolean;
  mensajeError?: string | null;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface DashboardActivityDTO {
  fecha: string;
  totalAcciones: number;
  accionesExitosas: number;
  accionesFallidas: number;
  accionesPorTipo: Record<string, number>;
  actividadesRecientes: ActividadRecienteDTO[];
}

export interface ActividadRecienteDTO {
  id: number;
  usuarioEmail: string;
  fechaHora: string;
  accion: string;
  tipoNombre: string;
  descripcion?: string;
  exitosa: boolean;
}

// ── Auditoría de Compras ────────────────────────────────────────────────────

export interface KpisAuditoriaComprasDTO {
  totalEventos: number;
  eventosExitosos: number;
  eventosFallidos: number;
  eventosPorAccion: Record<string, number>;
  ordenesConErrorErp: number;
  totalDevoluciones: number;
  valorTotalComprado: number;
}

export interface ReporteAuditoriaComprasDTO {
  kpis: KpisAuditoriaComprasDTO;
  logs: PaginatedResult<ActivityLogFullDTO>;
}

// ── Auditoría de Ventas ─────────────────────────────────────────────────────

export interface KpisAuditoriaVentasDTO {
  totalEventos: number;
  eventosExitosos: number;
  eventosFallidos: number;
  eventosPorAccion: Record<string, number>;
  totalVentas: number;
  totalAnulaciones: number;
  totalDevoluciones: number;
  valorTotalVendido: number;
  valorTotalAnulado: number;
  valorTotalDevuelto: number;
}

export interface ReporteAuditoriaVentasDTO {
  kpis: KpisAuditoriaVentasDTO;
  logs: PaginatedResult<ActivityLogFullDTO>;
}

export interface CambioEntidadDTO {
  id: number;
  fechaHora: string;
  usuarioEmail: string;
  usuarioNombre?: string;
  accion: string;
  descripcion?: string;
  datosAnteriores?: string;
  datosNuevos?: string;
  exitosa: boolean;
}

export interface HistorialEntidadDTO {
  tipoEntidad: string;
  entidadId: string;
  entidadNombre?: string;
  totalCambios: number;
  cambios: CambioEntidadDTO[];
}

// ─── Facturación Electrónica DIAN ─────────────────────────────────────────

export interface ConfiguracionEmisorDTO {
  id: number;
  sucursalId: number;
  nombreSucursal: string;
  nit: string;
  digitoVerificacion: string;
  razonSocial: string;
  nombreComercial: string;
  direccion: string;
  codigoMunicipio: string;
  codigoDepartamento: string;
  telefono: string;
  email: string;
  codigoCiiu: string;
  perfilTributario: string;
  numeroResolucion: string;
  fechaResolucion: string;
  prefijo: string;
  numeroDesde: number;
  numeroHasta: number;
  numeroActual: number;
  fechaVigenciaDesde: string;
  fechaVigenciaHasta: string;
  ambiente: string; // "1"=Producción, "2"=Pruebas
  pinSoftware: string;
  idSoftware: string;
  tieneCertificado: boolean;
}

export interface ActualizarConfiguracionEmisorDTO {
  nit: string;
  digitoVerificacion: string;
  razonSocial: string;
  nombreComercial: string;
  direccion: string;
  codigoMunicipio: string;
  codigoDepartamento: string;
  telefono: string;
  email: string;
  codigoCiiu: string;
  perfilTributario: string;
  numeroResolucion: string;
  fechaResolucion: string;
  prefijo: string;
  numeroDesde: number;
  numeroHasta: number;
  fechaVigenciaDesde: string;
  fechaVigenciaHasta: string;
  ambiente: string;
  pinSoftware: string;
  idSoftware: string;
  certificadoBase64?: string;
  certificadoPassword?: string;
}

export interface DocumentoElectronicoDTO {
  id: number;
  ventaId?: number;
  sucursalId: number;
  nombreSucursal: string;
  tipoDocumento: string; // "FV" | "NC" | "ND"
  prefijo: string;
  numero: number;
  numeroCompleto: string;
  cufe: string;
  fechaEmision: string;
  estado: string; // "Pendiente" | "Generado" | "Firmado" | "Enviado" | "Aceptado" | "Rechazado"
  codigoEstado: number;
  fechaEnvioDian?: string;
  codigoRespuestaDian?: string;
  mensajeRespuestaDian?: string;
  intentos: number;
  fechaCreacion: string;
}

export interface DianRespuestaDTO {
  esValido: boolean;
  codigo: string;
  descripcion: string;
}

// ─── Kardex de Inventario ─────────────────────────────────────────

export interface ReporteKardexDTO {
  productoId: string;
  codigoBarras: string;
  nombre: string;
  sucursalId: number;
  nombreSucursal: string;
  fechaDesde: string;
  fechaHasta: string;
  saldoInicial: number;
  saldoFinal: number;
  costoPromedioVigente: number;
  movimientos: KardexMovimientoDTO[];
}

export interface KardexMovimientoDTO {
  fecha: string;
  tipoMovimiento: string;
  referencia: string;
  observaciones: string | null;
  entrada: number;
  salida: number;
  saldoAcumulado: number;
  costoUnitario: number;
  costoTotalMovimiento: number;
}

// ─── Capa 3 — Contexto de turno POS ──────────────────────────────────────

export interface ClienteRecienteDTO {
  id: number;
  nombre: string;
  identificacion?: string;
  ultimaVenta: string; // ISO date
}

export interface OrdenPendienteResumenDTO {
  id: number;
  numeroOrden: string;
  nombreProveedor: string;
  fechaOrden: string;
  fechaEntregaEsperada?: string;
  total: number;
  itemsCount: number;
}

export interface TurnContextDTO {
  clientesRecientes: ClienteRecienteDTO[];
  ordenesPendientes: OrdenPendienteResumenDTO[];
}

// ─── Capa 10 — Explicabilidad ────────────────────────────────────────────────

export interface AutomaticActionDTO {
  tipoAccion:       string;
  productoId?:      string;
  nombreProducto:   string;
  description:      string;
  reason:           string;       // "Por qué" la sugerencia es relevante
  dataSource:       string;       // Fuente de datos que respalda la sugerencia
  confidence:       number;       // 0.0 – 1.0
  canOverride:      boolean;
  cantidadSugerida?: number;
  diasRestantes?:   number;
}

// ─── Capa 4 — Historial de cliente ───────────────────────────────────────────

export interface ProductoFrecuenteDTO {
  productoId:    string;
  nombreProducto: string;
  cantidadTotal: number;
}

export interface ClienteHistorialDTO {
  clienteId:          number;
  totalCompras:       number;
  totalGastado:       number;
  gastoPromedio:      number;
  primeraVisita?:     string;
  ultimaVisita?:      string;
  topProductos:       ProductoFrecuenteDTO[];
  visitasPorDiaSemana: Record<string, number>;
  visitasPorHora:     Record<string, number>;
}

// ─── Multi-empresa ────────────────────────────────────────────────────────────

export interface EmpresaDTO {
  id:                 number;
  nombre:             string;
  nit?:               string;
  razonSocial?:       string;
  activo:             boolean;
  fechaCreacion?:     string;
  cantidadSucursales?: number;
}

export interface CrearEmpresaDTO {
  nombre:       string;
  nit?:         string;
  razonSocial?: string;
}

export interface ActualizarEmpresaDTO {
  nombre:       string;
  nit?:         string;
  razonSocial?: string;
  activo:       boolean;
}

export interface ConfiguracionVariableDTO {
  id: number;
  nombre: string;
  valor: string;
  descripcion?: string;
  activo: boolean;
  fechaCreacion: string;
  empresaId: number;
}

export interface CrearConfiguracionVariableDTO {
  nombre: string;
  valor: string;
  descripcion?: string;
}

export interface ActualizarConfiguracionVariableDTO {
  nombre: string;
  valor: string;
  descripcion?: string;
}
