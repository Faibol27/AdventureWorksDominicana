using AdventureWorksDominicana.Data.Context;
using AdventureWorksDominicana.Data.Models;
using Aplicada1.Core;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace AdventureWorksDominicana.Services;

public class ShiftService(IDbContextFactory<Contexto> DbFactory) : IService<Shift, byte>
{
    public Task<Shift?> Buscar(byte id)
    {
        throw new NotImplementedException();
    }

    public Task<bool> Eliminar(byte id)
    {
        throw new NotImplementedException();
    }

    public async Task<List<Shift>> GetList(Expression<Func<Shift, bool>> criterio)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.Shifts.Where(criterio).AsNoTracking().ToListAsync();
    }

    public Task<bool> Guardar(Shift entidad)
    {
        throw new NotImplementedException();
    }
}
