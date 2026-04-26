using AdventureWorksDominicana.Data.Context;
using AdventureWorksDominicana.Data.Models;
using Microsoft.EntityFrameworkCore;
using Aplicada1.Core;
using System.Linq.Expressions;

namespace AdventureWorksDominicana.Services;

public class ProductCategoryService(IDbContextFactory<Contexto> DbFactory) : IService<ProductCategory, int>
{
    public async Task<ProductCategory?> Buscar(int id)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.ProductCategories
            .Include(p => p.ProductSubcategories)
            .FirstOrDefaultAsync(p => p.ProductCategoryId == id);
    }

    public async Task<bool> Eliminar(int id)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        try
        {
            await contexto.ProductSubcategories
                .Where(s => s.ProductCategoryId == id)
                .ExecuteDeleteAsync();

            return await contexto.ProductCategories
                .Where(p => p.ProductCategoryId == id)
                .ExecuteDeleteAsync() > 0;
        }
        catch (DbUpdateException ex)
        {
            throw new ProductDependentDataException("No se puede eliminar la categoría porque hay productos vinculados a sus subcategorías.", ex);
        }
        catch { return false; }
    }

    public async Task<List<ProductCategory>> GetList(Expression<Func<ProductCategory, bool>> criterio)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.ProductCategories.Include(p => p.ProductSubcategories).Where(criterio).ToListAsync();
    }

    public async Task<bool> Guardar(ProductCategory entidad)
    {
        if (!await Existe(entidad.ProductCategoryId))
        {
            return await Insertar(entidad);
        }
        else
        {
            return await Modificar(entidad);
        }
    }
    public async Task<bool> Insertar(ProductCategory entidad)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        entidad.Rowguid = Guid.NewGuid();
        entidad.ModifiedDate = DateTime.Now;
        if(entidad.ProductSubcategories != null)
        {
            foreach (var sub in entidad.ProductSubcategories)
            {
                if(sub.Rowguid == Guid.Empty) sub.Rowguid = Guid.NewGuid();
                sub.ModifiedDate = DateTime.Now;
            }
        }
        contexto.ProductCategories.Add(entidad);
        return await contexto.SaveChangesAsync() > 0;
    }
    public async Task<bool> Existe(int idEntidad)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        return await contexto.ProductCategories.AnyAsync(p => p.ProductCategoryId == idEntidad);
    }
    public async Task<bool> Modificar(ProductCategory entidad)
    {
        await using var contexto = await DbFactory.CreateDbContextAsync();
        entidad.ModifiedDate = DateTime.Now;
        if(entidad.ProductSubcategories != null)
        {
            foreach (var sub in entidad.ProductSubcategories)
            {
                if(sub.Rowguid == Guid.Empty) sub.Rowguid = Guid.NewGuid();
                sub.ModifiedDate = DateTime.Now;
            }
        }
        contexto.Update(entidad);
        return await contexto.SaveChangesAsync() > 0;
    }
}
