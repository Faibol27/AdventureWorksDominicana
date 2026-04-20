using AdventureWorksDominicana.Data.Context;
using AdventureWorksDominicana.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AdventureWorksDominicana.Services;

public class ProductPhotoService(IDbContextFactory<Contexto> DbContextFactory)
{
    public async Task<bool> AsignarFotoPrincipal(int productId, string rutaOUrl)
    {
        await using var contexto = await DbContextFactory.CreateDbContextAsync();

        var fotoPrincipalActual = await contexto.ProductProductPhotos
            .Include(ppp => ppp.ProductPhoto)
            .FirstOrDefaultAsync(ppp => ppp.ProductId == productId && ppp.Primary);

        if (fotoPrincipalActual != null && fotoPrincipalActual.ProductPhoto != null)
        {
            fotoPrincipalActual.ProductPhoto.ThumbNailPhoto = null;
            fotoPrincipalActual.ProductPhoto.LargePhoto = null;
            fotoPrincipalActual.ProductPhoto.ThumbnailPhotoFileName = rutaOUrl;
            fotoPrincipalActual.ProductPhoto.LargePhotoFileName = rutaOUrl;
            fotoPrincipalActual.ProductPhoto.ModifiedDate = DateTime.Now;

            fotoPrincipalActual.Primary = true;
            fotoPrincipalActual.ModifiedDate = DateTime.Now;

            return await contexto.SaveChangesAsync() > 0;
        }

        var nuevaFoto = new ProductPhoto
        {
            ThumbNailPhoto = null,
            ThumbnailPhotoFileName = rutaOUrl,
            LargePhoto = null,
            LargePhotoFileName = rutaOUrl,
            ModifiedDate = DateTime.Now
        };

        contexto.ProductPhotos.Add(nuevaFoto);
        await contexto.SaveChangesAsync();

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
            .FirstOrDefaultAsync(ppp => ppp.ProductId == productId && ppp.Primary);

        return nexo?.ProductPhoto;
    }
}