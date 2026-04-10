using AdventureWorksDominicana.Data.Context;
using AdventureWorksDominicana.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AdventureWorksDominicana.Services;

public class ProductPhotoService(IDbContextFactory<Contexto> DbContextFactory)
{
    public async Task<bool> AsignarFotoPrincipal(int productId, byte[] fotoData, string nombreArchivo)
    {
        await using var contexto = await DbContextFactory.CreateDbContextAsync();

        var nuevaFoto = new ProductPhoto
        {
            ThumbNailPhoto = fotoData,
            ThumbnailPhotoFileName = nombreArchivo,
            LargePhoto = fotoData,
            LargePhotoFileName = nombreArchivo,
            ModifiedDate = DateTime.Now
        };

        contexto.ProductPhotos.Add(nuevaFoto);
        await contexto.SaveChangesAsync(); 

        var fotosAnteriores = await contexto.ProductProductPhotos
            .Where(ppp => ppp.ProductId == productId)
            .ToListAsync();

        foreach (var f in fotosAnteriores)
        {
            f.Primary = false;
        }

        var puente = new ProductProductPhoto
        {
            ProductId = productId,
            ProductPhotoId = nuevaFoto.ProductPhotoId,
            Primary = true, 
            ModifiedDate = DateTime.Now
        };

        contexto.ProductProductPhotos.Add(puente);
        return await contexto.SaveChangesAsync() > 0;
    }

    public async Task<ProductPhoto?> ObtenerFotoPrincipal(int productId)
    {
        await using var contexto = await DbContextFactory.CreateDbContextAsync();

        var nexo = await contexto.ProductProductPhotos
            .Include(ppp => ppp.ProductPhoto)
            .FirstOrDefaultAsync(ppp => ppp.ProductId == productId && ppp.Primary == true);

        return nexo?.ProductPhoto;
    }
}
