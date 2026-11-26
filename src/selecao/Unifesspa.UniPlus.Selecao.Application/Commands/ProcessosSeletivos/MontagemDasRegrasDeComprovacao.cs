namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Compõe as regras informadas e propaga o grupo de repetição às regras contidas.</summary>
internal sealed class MontagemDasRegrasDeComprovacao(MontagemDoDocumentoExigido documentos)
{
    /// <param name="Regras">As regras montadas; falha com todas as recusas, inclusive as de vínculo do tipo de documento.</param>
    /// <param name="VinculosDosTipos">Recusas de tipo de documento como vínculo novo. Não impedem a montagem: as regras vêm
    /// em <paramref name="Regras"/> para que o catálogo confira também os dados das mesmas exigências.</param>
    internal sealed record Montadas(Result<List<RegraDeComprovacao>> Regras, IReadOnlyList<FieldError> VinculosDosTipos);

    public async Task<Montadas> MontarRegrasSemGrupoAsync(
        IReadOnlyList<RegraInformadaConferida> entradas, int ordemInicial, CancellationToken cancellationToken)
    {
        RegrasMontadas montados = await MontarRegrasAsync(entradas, ordemInicial, cancellationToken).ConfigureAwait(false);
        List<FieldError> recusas = [.. montados.Declaracao, .. montados.Cadastros, .. montados.Composicao];
        return new(
            recusas.Count > 0 ? Result<List<RegraDeComprovacao>>.ValidationFailure([.. recusas, .. montados.Vinculos])
                : Result<List<RegraDeComprovacao>>.Success(montados.Regras),
            montados.Vinculos);
    }

    private async Task<RegrasMontadas> MontarRegrasAsync(
        IReadOnlyList<RegraInformadaConferida> entradas, int ordemInicial, CancellationToken cancellationToken)
    {
        RegrasMontadas montados = new();
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            await MontarRegraAsync(entradas[indice], ordemInicial + indice, montados, cancellationToken).ConfigureAwait(false);
        }
        return montados;
    }

    private async Task MontarRegraAsync(RegraInformadaConferida regra, int ordem, RegrasMontadas destino, CancellationToken cancellationToken)
    {
        destino.Declaracao.AddRange(regra.Recusas);
        Result<MontagemDoDocumentoExigido.Partes>? partes = await ConferirDocumentoAsync(regra, destino, cancellationToken).ConfigureAwait(false);
        RegrasMontadas itens = await MontarRegrasAsync(regra.Itens, 0, cancellationToken).ConfigureAwait(false);
        destino.IncorporarErros(itens);
        bool composicaoRecusada = ConferirComposicao(regra, itens, destino);
        if (regra.Recusas.Count > 0 || partes?.IsFailure == true || !itens.TodosMontados || itens.TemErros || regra.Repeticao.Recusada || composicaoRecusada)
        {
            destino.TodosMontados = false;
            return;
        }
        ConstruirRegra(regra, ordem, partes, itens.Regras, destino);
    }

    private async Task<Result<MontagemDoDocumentoExigido.Partes>?> ConferirDocumentoAsync(
        RegraInformadaConferida regra, RegrasMontadas destino, CancellationToken cancellationToken)
    {
        if (regra.Declaracao.Forma != FormaDeComprovacao.Documento || regra.Entrada.Documento is not { } documento)
        {
            return null;
        }
        Result<MontagemDoDocumentoExigido.Partes> partes = await documentos.ConferirReferenciasAsync(documento, regra.Repeticao, cancellationToken).ConfigureAwait(false);
        IEnumerable<FieldError> erros = partes.IsFailure ? partes.Errors : partes.Value!.VinculoDoTipo.Errors;
        foreach (FieldError erro in ErrosDoDocumento(erros, regra))
        {
            (EhVinculoDoTipo(erro) ? destino.Vinculos : destino.Cadastros).Add(erro);
        }
        return partes;
    }

    private static bool ConferirComposicao(RegraInformadaConferida regra, RegrasMontadas itens, RegrasMontadas destino)
    {
        if (!itens.TodosMontados || itens.Regras.Count == 0 || regra.Declaracao.Forma is not (FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens))
        {
            return false;
        }
        List<FieldError> erros = [.. RegraDeComprovacao.ConferirComposicao(itens.Regras,
            regra.Repeticao.Recusada ? null : regra.Entrada.GrupoDoFormularioParaRepeticao).Select(erro => ComMensagemDoItem(erro.NoCaminhoDe(regra.Caminho), regra.Indice))];
        destino.Composicao.InsertRange(destino.Composicao.Count - itens.Composicao.Count, erros);
        return erros.Count > 0;
    }

    private void ConstruirRegra(RegraInformadaConferida regra, int ordem, Result<MontagemDoDocumentoExigido.Partes>? partes,
        List<RegraDeComprovacao> itens, RegrasMontadas destino)
    {
        Result<RegraDeComprovacao> resultado = partes is null ? CriarGrupo(regra.Entrada, ordem, regra.Declaracao, itens)
            : CriarRegraDocumento(regra, ordem, partes.Value!);
        if (resultado.IsFailure)
        {
            destino.Composicao.AddRange(resultado.Errors.Select(erro => ComMensagemDoItem(erro.NoCaminhoDoItem(regra.Caminho), regra.Indice)));
            destino.TodosMontados = false;
        }
        else
        {
            destino.Regras.Add(resultado.Value!);
        }
    }

    private Result<RegraDeComprovacao> CriarRegraDocumento(RegraInformadaConferida regra, int ordem, MontagemDoDocumentoExigido.Partes partes)
    {
        Result<DocumentoExigido> documento = documentos.Criar(regra.Entrada.Documento!, partes);
        if (documento.IsFailure)
        {
            return Result<RegraDeComprovacao>.ValidationFailure([.. documento.Errors.Select(e => e.NoCaminhoDoItem("documento"))]);
        }
        DistincaoDeclarada? distincao = regra.Declaracao.Distincao;
        return RegraDeComprovacao.CriarRegraDeDocumento(documento.Value!, ordem, regra.Declaracao.QuantidadeMinima,
            distincao?.Criterio, distincao?.DataReferencia, distincao?.OcorrenciasEsperadas, regra.Entrada.GrupoDoFormularioParaRepeticao);
    }

    // As recusas de vínculo do tipo ficam juntas, na ordem das exigências, qualquer que seja o resultado das outras referências.
    private static bool EhVinculoDoTipo(FieldError erro) =>
        erro.Error.Code == ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente;

    private static IEnumerable<FieldError> ErrosDoDocumento(IEnumerable<FieldError> erros, RegraInformadaConferida regra) =>
        erros.Select(erro => ComMensagemDoItem(erro.NoCaminhoDoItem("documento").NoCaminhoDe(regra.Caminho), regra.Indice));

    private static FieldError ComMensagemDoItem(FieldError erro, int indice) =>
        erro with { Error = erro.Error with { Message = $"No item {indice + 1} da lista: {erro.Error.Message}" } };

    private sealed class RegrasMontadas
    {
        internal List<RegraDeComprovacao> Regras { get; } = [];
        internal List<FieldError> Declaracao { get; } = [];
        internal List<FieldError> Cadastros { get; } = [];
        internal List<FieldError> Composicao { get; } = [];
        internal List<FieldError> Vinculos { get; } = [];
        internal bool TodosMontados { get; set; } = true;
        internal bool TemErros => Declaracao.Count + Cadastros.Count + Composicao.Count > 0;
        internal void IncorporarErros(RegrasMontadas itens)
        {
            Declaracao.AddRange(itens.Declaracao);
            Cadastros.AddRange(itens.Cadastros);
            Composicao.AddRange(itens.Composicao);
            Vinculos.AddRange(itens.Vinculos);
        }
    }

    private static Result<RegraDeComprovacao> CriarGrupo(RegraDeComprovacaoInformada regra, int ordem, DeclaracaoDaRegraDeComprovacao declaracao, IReadOnlyList<RegraDeComprovacao> itens)
    {
        Result<IReadOnlyList<BaseLegalDaComprovacao>> basesLegais = BasesLegaisDoGrupo(regra.BasesLegaisDoGrupo());
        return basesLegais.IsFailure
            ? Result<RegraDeComprovacao>.ValidationFailure(basesLegais.Errors)
            : RegraDeComprovacao.CriarGrupo(
                new(declaracao.Forma!.Value, declaracao.QuantidadeMinima, declaracao.ConsequenciaDoDescumprimento, regra.Indeferimento?.AbrePrazoParaReenvio ?? false), ordem, basesLegais.Value!, itens, regra.GrupoDoFormularioParaRepeticao);
    }

    private static Result<IReadOnlyList<BaseLegalDaComprovacao>> BasesLegaisDoGrupo(IReadOnlyList<BaseLegalInput> declaradas) =>
        EscritaDasBasesLegais.Resolver(
            declaradas,
            static b => BaseLegalDaComprovacao.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b)),
            static b => BaseLegalDaComprovacao.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b), b.Observacao).Value!);
}
