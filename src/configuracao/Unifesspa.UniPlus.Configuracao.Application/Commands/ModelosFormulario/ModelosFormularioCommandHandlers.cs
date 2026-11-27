namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Lê e confere o conteúdo contra o catálogo e só então cria o modelo, com as recusas da leitura, do
/// catálogo e do modelo no mesmo lote (ADR-0125). A parte do conteúdo que não se leu não alimenta a
/// conferência que depende dela; as demais regras e as do cadastro saem junto.
/// </summary>
public static class CriarModeloFormularioCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        ITermoConsentimentoReader termoReader,
        ITipoProcessoReader tipoProcessoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(termoReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        EscritaDoModelo escrita = new(fatoRepository, termoReader, tipoProcessoReader);
        Result<ModeloFormulario> novo = await ConferirECriarAsync(command, escrita, cancellationToken).ConfigureAwait(false);
        return novo.IsFailure
            ? Result<Guid>.ValidationFailure(novo.Errors)
            : await GravacaoDoModelo.GravarNovoAsync(novo.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ModeloFormulario>> ConferirECriarAsync(
        CriarModeloFormularioCommand command, EscritaDoModelo escrita, CancellationToken cancellationToken)
    {
        DeclaracaoDoFormulario? declaracao = command.Declaracao?.ParaDeclaracao();
        DescritivoDoModelo descritivo = (command.Descritivo ?? DescritivoDoModeloInput.Ausente).ParaDescritivo();
        (ConteudoLido conteudo, CatalogoDoModelo catalogo, List<FieldError> erros) = await escrita.LerEConferirAsync(
            command.Conteudo, declaracao, descritivo.TipoProcessoCodigo, ModeloGravado.Nenhum, cancellationToken).ConfigureAwait(false);
        erros.AddRange(conteudo.NasPosicoesDaEntrada(ModeloFormulario.ConferirCriacao(
            command.Codigo, descritivo, declaracao, conteudo.ParaConteudo(), catalogo.Derivacoes, conteudo.PorInteiro)));
        return erros.Count > 0
            ? Result<ModeloFormulario>.ValidationFailure(erros)
            : ModeloFormulario.Criar(command.Codigo, descritivo, declaracao, conteudo.ParaConteudo(), catalogo.Derivacoes);
    }
}

/// <summary>
/// Como a criação, com o modelo gravado como referência: o vínculo a fato, valor ou tipo de
/// processo desativado só é recusado quando é novo (ADR-0136).
/// </summary>
public static class AtualizarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        AtualizarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        ITermoConsentimentoReader termoReader,
        ITipoProcessoReader tipoProcessoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(termoReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        EscritaDoModelo escrita = new(fatoRepository, termoReader, tipoProcessoReader);
        return GravacaoDoModelo.MutarAsync(
            command.Id, modelo => ConferirEAtualizarAsync(command, modelo, escrita, cancellationToken), repository, unitOfWork, cancellationToken);
    }

    private static async Task<Result> ConferirEAtualizarAsync(
        AtualizarModeloFormularioCommand command, ModeloFormulario modelo, EscritaDoModelo escrita, CancellationToken cancellationToken)
    {
        DescritivoDoModelo descritivo = (command.Descritivo ?? DescritivoDoModeloInput.Ausente).ParaDescritivo();
        (ConteudoLido conteudo, CatalogoDoModelo catalogo, List<FieldError> erros) = await escrita.LerEConferirAsync(
            command.Conteudo, modelo.Declaracao, descritivo.TipoProcessoCodigo, ModeloGravado.De(modelo), cancellationToken).ConfigureAwait(false);
        erros.AddRange(conteudo.NasPosicoesDaEntrada(modelo.ConferirAtualizacao(
            descritivo, conteudo.ParaConteudo(), catalogo.Derivacoes, conteudo.PorInteiro)));
        return erros.Count > 0
            ? Result.ValidationFailure(erros)
            : modelo.Atualizar(descritivo, conteudo.ParaConteudo(), catalogo.Derivacoes);
    }
}

public static class AtivarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        AtivarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return GravacaoDoModelo.MutarAsync(command.Id, static m => Task.FromResult(m.Ativar()), repository, unitOfWork, cancellationToken);
    }
}

public static class DesativarModeloFormularioCommandHandler
{
    public static Task<Result> Handle(
        DesativarModeloFormularioCommand command,
        IModeloFormularioRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return GravacaoDoModelo.MutarAsync(command.Id, static m => Task.FromResult(m.Desativar()), repository, unitOfWork, cancellationToken);
    }
}
