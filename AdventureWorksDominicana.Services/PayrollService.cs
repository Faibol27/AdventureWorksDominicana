using AdventureWorksDominicana.Data.Context;
using AdventureWorksDominicana.Data.Models;
using Aplicada1.Core;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AdventureWorksDominicana.Services;

public class PayrollService(IDbContextFactory<Contexto> DbFactory) : IService<Payroll, int>
{
    public async Task<bool> Guardar(Payroll entidad)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();

        foreach (var detalle in entidad.PayrollDetails)
        {
            detalle.Employee = null;
        }

        if (entidad.PayrollId == 0)
        {
            entidad.CreatedDate = DateTime.Now;
            contexto.Payrolls.Add(entidad);
        }
        else
        {
            contexto.Payrolls.Update(entidad);
        }

        return await contexto.SaveChangesAsync() > 0;
    }

    public async Task<Payroll?> Buscar(int id)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.Payrolls
            .Include(p => p.PayrollDetails)
                .ThenInclude(d => d.Employee)
                    .ThenInclude(e => e.BusinessEntity) // Para traer nombres
            .FirstOrDefaultAsync(p => p.PayrollId == id);
    }

    public async Task<List<Payroll>> GetList(Expression<Func<Payroll, bool>> criterio)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.Payrolls.Where(criterio).ToListAsync();
    }

    public async Task<bool> Eliminar(int id)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.Payrolls.Where(p => p.PayrollId == id).ExecuteDeleteAsync() > 0;
    }

    public async Task ProcesarNomina(Payroll nominaBorrador)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();

        var parametros = await contexto.PayrollParameters.AsNoTracking().FirstOrDefaultAsync(p => p.IsActive);
        if (parametros == null) throw new Exception("No hay parámetros de nómina activos.");

        // Todos los empleados, para reducir las consultas.
        var empleados = await contexto.Employees
            .AsNoTracking()
            .Include(e => e.BusinessEntity)
            .Where(e => e.CurrentFlag)
            .ToListAsync();

        decimal topeSFS = parametros.MinimumWage * 10;
        decimal topeAFP = parametros.MinimumWage * 20;

        int diasLaborables = CalcularDiasLaborables(nominaBorrador.PeriodStartDate, nominaBorrador.PeriodEndDate);

        foreach (var emp in empleados)
        {
            var sueldoActual = await contexto.EmployeePayHistories
                .AsNoTracking()
                .Where(h => h.BusinessEntityId == emp.BusinessEntityId)
                .OrderByDescending(h => h.RateChangeDate) //Se toma el ultimo modificado
                .FirstOrDefaultAsync();

            if (sueldoActual == null || sueldoActual.Rate <= 0) continue;

            DateOnly finPeriodo = DateOnly.FromDateTime(nominaBorrador.PeriodEndDate);
            //Tanda
            var asignacionDepto = await contexto.EmployeeDepartmentHistories
                .AsNoTracking()
                .Include(ed => ed.Shift)
                .Where(ed => ed.BusinessEntityId == emp.BusinessEntityId &&
                            (ed.EndDate == null || ed.EndDate >= finPeriodo))
                .FirstOrDefaultAsync();

            // VALIDACIÓN CON NOMBRE YA CARGADO
            if (asignacionDepto?.Shift == null)
            {
                string nombreEmpleado = emp.BusinessEntity != null
                    ? $"{emp.BusinessEntity.FirstName} {emp.BusinessEntity.LastName}"
                    : $"ID: {emp.BusinessEntityId}";

                throw new Exception($"Error: El empleado {nombreEmpleado} no tiene una tanda (Shift) asignada.");
            }

            TimeSpan jornada = asignacionDepto.Shift.EndTime - asignacionDepto.Shift.StartTime; //TimeSpan hace un calculo exacto para un pago de horas preciso
            decimal horasDiarias = (decimal)jornada.TotalHours;
            if (horasDiarias > 5) horasDiarias -= 1; //Se le resta el armuerzo, legal por articulo.

            // CÁLCULOS 
            decimal totalHorasTrabajadas = horasDiarias * diasLaborables;
            decimal sueldoBrutoPeriodo = sueldoActual.Rate * totalHorasTrabajadas;

            // Proyección mensual (23.83 es el factor de ley en RD)
            decimal sueldoMensualProyectado = (sueldoActual.Rate * horasDiarias) * 23.83m;

            decimal baseSFS = Math.Min(sueldoMensualProyectado, topeSFS); //Se queda con el mas pequeño por el tope de cotización
            decimal baseAFP = Math.Min(sueldoMensualProyectado, topeAFP);

            decimal descuentoSFSMensual = baseSFS * parametros.SfsPct;
            decimal descuentoAFPMensual = baseAFP * parametros.AfpPct;

            decimal sueldoNetoAntesISR = sueldoMensualProyectado - descuentoSFSMensual - descuentoAFPMensual; //Por ley, el dinero que el empleado paga para su seguro (SFS) y su pensión (AFP) está exento de impuestos
            decimal isrMensual = CalcularISR(sueldoNetoAntesISR, parametros.IsrAnnualExemption);

            // Proporción del periodo
            decimal proporcion = (decimal)diasLaborables / 23.83m; //En la legislación laboral dominicana (Resolución 80-92), un mes no se cuenta como 30 días para fines de pago de sueldo, sino como 23.83 días laborables.

            decimal dSFS = descuentoSFSMensual * proporcion;
            decimal dAFP = descuentoAFPMensual * proporcion;
            decimal dISR = isrMensual * proporcion;

            nominaBorrador.PayrollDetails.Add(new PayrollDetail
            {
                BusinessEntityId = emp.BusinessEntityId,
                GrossSalary = sueldoBrutoPeriodo,
                SfsDeduction = dSFS,
                AfpDeduction = dAFP,
                IsrDeduction = dISR,
                NetSalary = sueldoBrutoPeriodo - dSFS - dAFP - dISR
            });
        }
    }

    // Método auxiliar para contar los días laborables (Lunes a Viernes) entre dos fechas
    private int CalcularDiasLaborables(DateTime fechaInicio, DateTime fechaFin)
    {
        int dias = 0;
        DateTime fechaActual = fechaInicio;
        while (fechaActual <= fechaFin)
        {
            // Si no es sábado ni domingo, sumamos un día laborable
            if (fechaActual.DayOfWeek != DayOfWeek.Saturday && fechaActual.DayOfWeek != DayOfWeek.Sunday)
            {
                dias++;
            }
            fechaActual = fechaActual.AddDays(1);
        }
        return dias > 0 ? dias : 1; // Mínimo 1 día para evitar división por cero
    }

    private decimal CalcularISR(decimal sueldoMensualNeto, decimal exencionAnual)
    {
        // Proyección anual
        decimal sueldoAnual = sueldoMensualNeto * 12;

        if (sueldoAnual <= exencionAnual) return 0; // Exento

        decimal isrAnual = 0;

        // Escalas de la DGII 
        decimal escala2 = 624329.00m; // 15%
        decimal escala3 = 867123.00m; // 20%

        if (sueldoAnual <= escala2)
        {
            isrAnual = (sueldoAnual - exencionAnual) * 0.15m;
        }
        else if (sueldoAnual <= escala3)
        {
            isrAnual = 31216.00m + ((sueldoAnual - escala2) * 0.20m);
        }
        else // Tope máximo (25%)
        {
            isrAnual = 79776.00m + ((sueldoAnual - escala3) * 0.25m);
        }

        return isrAnual / 12; // Devolver valor mensual
    }
}