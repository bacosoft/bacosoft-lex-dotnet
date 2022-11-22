using Bacosoft;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;

namespace bacosoft_lex_dotnet_samples
{
    class Program
    {
        private static ServiceClient client;

        private static long? idBodega;

        private static long? idEstablecimiento;

        private static IConfigurationSection config;

        /// <summary>
        /// Carga la configuración con la información de conexión para realizar las pruebas.
        /// Debe crearse un fichero bacosoft-lex-dotnet-samples.json en la carpeta %home% del usuario.
        /// 
        /// Este es un ejemplo del contenido del fichero:
        /// <code>
        /// {
        ///  "ClientSettings": {
        ///    "baseUrl": "https://...",
        ///    "tenant": "códigoBaseDatos",
        ///    "userName": "nombreUsuario",
        ///    "password": "contraseña"
        ///  }
        /// }
        /// </code>
        /// </summary>
        private static void LoadCustomConfiguration()
        {
            config = new ConfigurationBuilder()
                 .SetBasePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                 .AddJsonFile("bacosoft-lex-dotnet-samples.json")
                 .Build().GetSection("ClientSettings");
        }

        private static string GetConfig(string key)
        {
            if (config == null)
            {
                LoadCustomConfiguration();
            }
            string? res = config[key];
            if (res == null)
            {
                throw new Exception($"{key} not fount in configuration file!");
            }
            return res;
        }

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            client = new ServiceClient
            {
                BaseUrl = GetConfig("baseUrl"),
                Tenant = GetConfig("tenant"),
                UserName = GetConfig("userName"),
                Password = GetConfig("password")
            };

            // obtener lista de establecimientos
            var doc = ConsultarEstablecimientosDelTenant();

            // me quedo con la primer empresa y establecimiento de la lista
            var nav = doc.CreateNavigator();
            idBodega = long.Parse(nav.SelectSingleNode("/PagingLoadResult/data/data/responsable/@id").Value);
            idEstablecimiento = long.Parse(nav.SelectSingleNode("/PagingLoadResult/data/data/@id").Value);

            // consulto existencias de esa empresa y establecimiento
            doc = ConsultarExistencias(DateTime.Today);

            // me quedo con la primera partida y le registramos una salida con documento interno
            nav = doc.CreateNavigator();
            ImportarSalidaInterna(DateTime.Today, nav.SelectSingleNode("/PagingLoadResult/data/data"));
        }

        private static void ImportarSalidaInterna(DateTime fecha, XPathNavigator partida)
        {
            string data;
            var cantidad = partida.SelectSingleNode("@cantidad").Value;
            var producto = partida.SelectSingleNode("partida/producto/@id").Value;
            var um = partida.SelectSingleNode("@unidadMedida").Value;

            data = $"<registros><registro _class=\"RegistroEntradaSalida\" cantidad=\"{cantidad}\" unidadMedida=\"{um}\" partida_producto=\"[id = {producto}]\" fecha=\"{fecha:dd/MM/yyyy}\" claseDocumento=\"INTERNO\" numeroDocumento=\"INV-{DateTime.Now}\" empresa=\"[id={idBodega}]\"></registro></registros>";
            Import(idBodega, idEstablecimiento, data, false);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"Unhandled exception: {e.ExceptionObject}");
        }

        private static void ImportarProductosSubordinados()
        {
            string data;
            data = @"<registros><entidad _class=""Producto"" id.erp=""00001"" nombre=""IPA 33CL"" subordinadoA=""Cerveza"" tipoPartida=""EMBOTELLADO""/><entidad _class=""Producto"" id.erp=""00002"" nombre=""IPA KK 30L"" subordinadoA=""Cerveza"" tipoPartida=""EMBOTELLADO""/></registros>";
            Import(null, null, data, false);
        }

        private static void ImportarProvinciasYPoblaciones()
        {
            string data;
            data = @"<registros><entidad _class=""Provincia"" id.erp=""00001"" nombre=""Francia"" pais=""[codigoIso=FR]""/><entidad _class=""Poblacion"" id.erp=""00001"" nombre=""Francia"" provincia=""[id.erp=00001]""/></registros>";
            Import(null, null, data, false);
        }

        private static void ImportarClientesYProveedores()
        {
            string data;
            data = @"<registros><entidad _class=""Persona"" id.cliente=""00001"" nif ="""" nombre =""BAR JUANITO"" /><entidad _class=""Persona"" id.cliente=""00002"" nif ="""" nombre =""BAR TOMEU"" /><entidad _class=""Persona"" id.proveedor=""00001"" nif ="""" nombre =""LA TIENDA DEL CERVECERO"" /></registros>";
            Import(null, null, data, false);
        }

        private static void ImportarClientesConErrorImportacionPorqueFaltanDatos()
        {
            string data;
            data = @"<registros><entidad _class=""Persona"" id.cliente=""00003"" nif ="""" nombre ="""" /><entidad _class=""Persona"" id.cliente=""00004"" nif ="""" nombre ="""" /></registros>";
            Import(null, null, data, false);
        }

        private static XmlDocument ConsultarExistencias(DateTime fecha)
        {
            string parameters;
            parameters = $"<ExistenciasFindParameters><bodega>{idBodega}</bodega><establecimiento>{idEstablecimiento}</establecimiento><projection>list</projection><fecha>{fecha:yyyy-MM-dd}</fecha></ExistenciasFindParameters>";
            return Query("/lex/api/partida/existencias", parameters);
        }

        private static void ConsultarMovimientosPrimeraVezAPartirFecha()
        {
            string parameters;
            parameters = $"<RegistroModificadoFindParameters><bodega>{idBodega}</bodega><establecimiento>{idEstablecimiento}</establecimiento><projection>list</projection><desdeFecha>2020-01-01</desdeFecha><mappingSetExcluido>erp</mappingSetExcluido></RegistroModificadoFindParameters>";
            XmlDocument xdoc = Query("/lex/api/registro/modificados", parameters);
            XmlNodeList elements = xdoc.GetElementsByTagName("ultimoIdAuditoria");
            if (elements.Count > 0)
            {
                Console.WriteLine("ultimoIdAuditoria = " + elements[0].InnerText);
            }
        }

        private static void ConsultarMovimientoAPartirIdAuditoria()
        {
            string parameters;
            parameters = $"<RegistroModificadoFindParameters><bodega>{idBodega}</bodega><establecimiento>{idEstablecimiento}</establecimiento><projection>list</projection><ultimoIdAuditoria>1810786</ultimoIdAuditoria><mappingSetExcluido>erp</mappingSetExcluido></RegistroModificadoFindParameters>";
            Query("/lex/api/registro/modificados", parameters);
        }

        private static XmlDocument ConsultarEstablecimientosDelTenant()
        {
            return Query("/lex/api/establecimiento/query", "<FindParameters><projection>list</projection></FindParameters>");
        }

        private static XmlDocument ConsultarEmpresasDelTenant()
        {
            return Query("/lex/api/persona/bodegas", "<FindParameters><projection>list</projection></FindParameters>");
        }

        private static XmlDocument Query(string recurso, string parametros)
        {
            XmlDocument res = new();
            PrintTestName();
            try
            {
                string resp = client.Query(recurso, parametros);
                Console.WriteLine(resp);
                res.LoadXml(resp);
            }
            catch (ServiceException e)
            {
                Console.WriteLine(e.GetType().Name + ": " + e.StatusCode + " - " + e.Timestamp + ": " + e.Message);
                throw e;
            }
            return res;
        }

        private static void Import(long? idEmpresa, long? idEstablecimiento, string datos, bool validar)
        {
            PrintTestName();
            try
            {
                string res = client.Import(idEmpresa, idEstablecimiento, datos, validar);
                Console.WriteLine(res);
            }
            catch (ServiceException e)
            {
                Console.WriteLine(e.GetType().Name + ": " + e.StatusCode + " - " + e.Timestamp + ": " + e.Message);
            }
            Console.WriteLine();
        }

        private static void PrintTestName()
        {
            StackTrace t = new StackTrace();
            Console.WriteLine("Test: " + t.GetFrame(t.FrameCount - 2).GetMethod().Name);
        }
    }
}
