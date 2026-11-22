namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

internal static class ArquiteturaDaSelecao
{
    internal static readonly Architecture Produto = new ArchLoader()
        .LoadAssemblies(
            typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
            typeof(Domain.Entities.ProcessoSeletivo).Assembly,
            typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
            typeof(Infrastructure.Persistence.SelecaoContribuicao).Assembly,
            typeof(API.Controllers.ProcessoSeletivoController).Assembly,
            typeof(global::Unifesspa.UniPlus.Infrastructure.Core.Storage.IStorageService).Assembly,
            typeof(global::Unifesspa.UniPlus.Kernel.Results.DomainError).Assembly,
            typeof(global::Unifesspa.UniPlus.Regras.Entradas.EntradaDeRegras).Assembly,
            typeof(ArquiteturaDaSelecao).Assembly,
            typeof(object).Assembly)
        .Build();
}
