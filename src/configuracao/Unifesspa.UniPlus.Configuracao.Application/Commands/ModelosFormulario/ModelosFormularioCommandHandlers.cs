namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Lê e confere o conteúdo contra o catálogo, e só então cria o modelo, com as recusas do catálogo
/// e as do modelo no mesmo lote (ADR-0125). A entrada que não se lê não chega às regras do conteúdo,
/// que dependem dele inteiro; as do cadastro saem junto.
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
        FinalidadeFormulario? finalidade = EstruturaFormulario.FinalidadeDoCodigo(command.Finalidade);
        DescritivoDoModeloInput descritivo = command.Descritivo ?? DescritivoDoModeloInput.Ausente;
        (ConteudoLido conteudo, IReadOnlyList<FieldError> recusasDaLeitura, CatalogoDoModelo catalogo, List<FieldError> erros) = await escrita.LerEConferirAsync(
            command.Conteudo, finalidade, descritivo.TipoProcessoCodigo, ModeloGravado.Nenhum, cancellationToken).ConfigureAwait(false);
        if (recusasDaLeitura.Count > 0)
        {
            erros.AddRange(ModeloFormulario.ConferirCadastro(command.Codigo, descritivo.Nome, descritivo.Descricao, descritivo.TipoProcessoCodigo, finalidade));
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        Result<ModeloFormulario> criar = ModeloFormulario.Criar(
            command.Codigo, descritivo.Nome, descritivo.Descricao, finalidade, descritivo.TipoProcessoCodigo, conteudo.ParaConteudo(), catalogo.Derivacoes);
        erros.AddRange(criar.Errors);
        return erros.Count > 0 ? Result<ModeloFormulario>.ValidationFailure(erros) : criar;
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
        DescritivoDoModeloInput descritivo = command.Descritivo ?? DescritivoDoModeloInput.Ausente;
        (ConteudoLido conteudo, IReadOnlyList<FieldError> recusasDaLeitura, CatalogoDoModelo catalogo, List<FieldError> erros) = await escrita.LerEConferirAsync(
            command.Conteudo, modelo.Finalidade, descritivo.TipoProcessoCodigo, ModeloGravado.De(modelo), cancellationToken).ConfigureAwait(false);
        if (recusasDaLeitura.Count > 0)
        {
            erros.AddRange(ModeloFormulario.ConferirDescritivo(descritivo.Nome, descritivo.Descricao, descritivo.TipoProcessoCodigo));
            return Result.ValidationFailure(erros);
        }

        Result atualizar = modelo.Atualizar(
            descritivo.Nome, descritivo.Descricao, descritivo.TipoProcessoCodigo, conteudo.ParaConteudo(), catalogo.Derivacoes);
        erros.AddRange(atualizar.Errors);
        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
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
