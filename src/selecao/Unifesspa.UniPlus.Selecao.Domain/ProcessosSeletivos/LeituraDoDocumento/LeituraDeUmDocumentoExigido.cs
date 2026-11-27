namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê uma exigência pelas fábricas do domínio e acumula todas as recusas independentes, na ordem em que orientam: a chave, as citações
/// (fase, etapa e tipo de documento), o formulário, a forma da exigência, as condições, as bases legais e os requisitos. A fábrica só é
/// chamada quando nada foi recusado nem cortado, porque ela exige a fase e o tipo já resolvidos. Os dados que o documento comprova são os que
/// o autor declarou, e a conferência de o processo os coletar é anterior ao congelamento. O modelo de documento não é lido.
/// </summary>
internal static class LeituraDeUmDocumentoExigido
{
    private const string CampoDoFormulario = "formularioCodigo";

    internal static ExigenciaLida Ler(ExigenciaNoDocumento exigencia, CadastrosCitadosNoDocumento cadastros, FasesLidas fases, EtapasLidas etapas)
    {
        Result<ChaveLocal> chave = ChaveLocal.Criar(exigencia.Chave);
        CitacoesDaExigencia citacoes = LeituraDasCitacoesDaExigencia.Ler(exigencia, cadastros, fases, etapas);
        PartesDaExigencia partes = LerPartes(exigencia, fases);
        FieldError[] recusas = [.. chave.Errors.Select(static recusa => recusa.NoCaminhoDoItem("chave")), .. citacoes.Recusas, .. partes.Recusas];

        return recusas.Length == 0 && citacoes.Completas && partes.Completas
            ? Criar(exigencia, chave.Value, citacoes, partes)
            : new(null, exigencia.PosicaoNoDocumento, exigencia.Chave, recusas);
    }

    private static PartesDaExigencia LerPartes(ExigenciaNoDocumento exigencia, FasesLidas fases)
    {
        Result<CodigoDoFormulario> formulario = CodigoDoFormulario.Criar(exigencia.FormularioCodigo);
        ParteLida<IReadOnlyList<CondicaoGatilho>> condicoes = LeituraDasCondicoesDaExigencia.Ler(exigencia.Condicoes);
        ParteLida<IReadOnlyList<DocumentoExigidoBaseLegal>> bases = LeituraDasBasesLegais.DoDocumento(exigencia.BasesLegais);
        ParteLida<RequisitosDoDocumento> requisitos = LeituraDosRequisitosDaExigencia.Ler(exigencia, fases);
        FieldError[] recusas =
        [
            .. formulario.Errors.Select(static recusa => recusa.NoCaminhoDoItem(CampoDoFormulario)),
            .. FormaDaExigencia(exigencia),
            .. condicoes.Recusas,
            .. bases.Recusas,
            .. requisitos.Recusas,
        ];

        return new(formulario.Value, condicoes.Valor, bases.Valor, requisitos.Valor, !requisitos.Cortada, recusas);
    }

    private static ExigenciaLida Criar(ExigenciaNoDocumento exigencia, ChaveLocal chave, CitacoesDaExigencia citacoes, PartesDaExigencia partes)
    {
        DocumentoExigidoDeclarado declarado = new(
            citacoes.Tipo!,
            new MomentoDaExigencia(citacoes.FaseId!.Value, citacoes.EtapaId, partes.Formulario!),
            new AplicabilidadeDaExigencia(exigencia.Aplicabilidade, partes.Condicoes!),
            new IndeferimentoDoDocumento(exigencia.Obrigatorio ?? false, CodigoDoValorLido.De(exigencia.ConsequenciaDoDescumprimento), partes.BasesLegais!, exigencia.AbrePrazoParaReenvio),
            partes.Requisitos!,
            exigencia.DadosComprovados);
        Result<DocumentoExigido> criado = DocumentoExigido.Criar(chave, declarado);

        return new(criado.Value, exigencia.PosicaoNoDocumento, exigencia.Chave, criado.Errors);
    }

    // A forma da exigência que a fábrica confere: a aplicabilidade, a consequência, os dados comprovados e o tamanho máximo.
    private static IEnumerable<FieldError> FormaDaExigencia(ExigenciaNoDocumento exigencia) =>
    [
        .. DocumentoExigido.ValidarFormaBasica(
            exigencia.Aplicabilidade,
            CodigoDoValorLido.De(exigencia.ConsequenciaDoDescumprimento),
            exigencia.Condicoes.Sum(static alternativa => alternativa.Count),
            exigencia.DadosComprovados),
        .. RequisitosDoDocumento.ValidarTamanho(exigencia.TamanhoMaximoBytes),
    ];
}

/// <summary>O formulário, as condições, as bases legais e os requisitos da exigência, nulos quando recusados, e as recusas na ordem em que orientam.</summary>
internal sealed record PartesDaExigencia(
    CodigoDoFormulario? Formulario,
    IReadOnlyList<CondicaoGatilho>? Condicoes,
    IReadOnlyList<DocumentoExigidoBaseLegal>? BasesLegais,
    RequisitosDoDocumento? Requisitos,
    bool SemCorte,
    IReadOnlyList<FieldError> Recusas)
{
    internal bool Completas => SemCorte && Formulario is not null && Condicoes is not null && BasesLegais is not null && Requisitos is not null;
}
