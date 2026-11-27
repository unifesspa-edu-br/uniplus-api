namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ExigenciasLidasDeTeste;

/// <summary>
/// A árvore de comprovação é lida em profundidade, com as folhas citando as exigências lidas pela chave; a recusa de um nó sai no ponteiro
/// do próprio nó, junto com as dos demais nós, e a exigência que a leitura recusou corta a folha sem causa nova.
/// </summary>
public sealed class LeituraDaArvoreDeComprovacaoTests
{
    private const string Raiz = "/documentosExigidos/regrasDeComprovacao";

    private static readonly ExigenciaNoDocumento[] DuasExigencias = [Exigencia(0, "identidade"), Exigencia(1, "residencia", tipo: Residencia)];

    [Fact(DisplayName = "A árvore que cita as duas exigências pela chave as adota, com a ordem pela posição mais 1 e o grupo contendo as folhas")]
    public void Arvore_CitaAsDuasExigencias()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Grupo(0, FormaDeComprovacao.TodosOsItens, Folha(0, "identidade"), Folha(1, "residencia")));

        Causas(lidas).Should().BeEmpty();
        RegraDeComprovacao[] regras = [.. lidas.Exigencias.RegrasDeComprovacao];
        regras.Should().HaveCount(3);
        regras.Where(static regra => regra.RegraQueContemId is null).Should().ContainSingle().Which.Ordem.Should().Be(1);
        regras.Where(static regra => regra.DocumentoExigido is not null).Select(static regra => regra.DocumentoExigido!.Chave.Valor).Should().BeEquivalentTo("identidade", "residencia");
    }

    [Fact(DisplayName = "Duas folhas que citam a mesma chave usam o mesmo documento exigido")]
    public void DuasFolhas_MesmaChave_MesmoDocumento()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Grupo(0, FormaDeComprovacao.QuantidadeMinimaDeItens, Folha(0, "identidade"), Folha(1, "identidade")).ComQuantidade(1));

        Causas(lidas).Should().BeEmpty();
        lidas.Exigencias.DocumentosExigidos.Should().HaveCount(2);
        lidas.Exigencias.RegrasDeComprovacao.Where(static regra => regra.DocumentoExigido is not null)
            .Select(static regra => regra.DocumentoExigido).Distinct().Should().ContainSingle();
    }

    [Fact(DisplayName = "A chave citada que não existe recusa o campo que cita, no nó, sem repetir o texto digitado")]
    public void Citacao_Inexistente_RecusaOCampoQueCita()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Folha(0, "digitada"));

        Causas(lidas).Should().Equal(("/documentosExigidos/regrasDeComprovacao/0/exigenciaChave", "RegraDeComprovacao.DocumentoObrigatorio"));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("digitada", StringComparison.Ordinal));
        lidas.Exigencias.RegrasDeComprovacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência recusada na leitura corta a folha que a cita, sem causa nova")]
    public void Citacao_DeExigenciaRecusada_NaoAcumulaCausaNova()
    {
        ExigenciaNoDocumento[] exigencias = [Exigencia(0, "identidade", tipo: "TIPO-DIGITADO")];

        ExigenciasLidas lidas = Ler(exigencias, Folha(0, "identidade"));

        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/0/tipoDocumentoCodigo", "DocumentoExigido.TipoDocumentoNaoEncontrado"));
        lidas.Exigencias.RegrasDeComprovacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A árvore devolve juntas as recusas de todos os nós inválidos, cada uma no ponteiro do seu nó, o pai antes dos filhos")]
    public void Arvore_RecusasDeTodosOsNos_Juntas()
    {
        ExigenciasLidas lidas = Ler(
            DuasExigencias,
            Grupo(0, FormaDeComprovacao.TodosOsItens, Folha(0, "digitada"), Folha(1, "identidade"), Folha(2, "outra")),
            Folha(1, "residencia"),
            Folha(2, "terceira"));

        Causas(lidas).Select(static causa => causa.Ponteiro).Should().Equal(
            $"{Raiz}/0/itens/0/exigenciaChave",
            $"{Raiz}/0/itens/2/exigenciaChave",
            $"{Raiz}/2/exigenciaChave");
    }

    [Fact(DisplayName = "A posição enviada orienta o ponteiro e a ordem, que não é renumerada quando um nó irmão é recusado")]
    public void Posicao_Enviada_OrientaPonteiroEOrdem()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Folha(0, "digitada"), Folha(1, "identidade"));

        lidas.Exigencias.RegrasDeComprovacao.Should().ContainSingle().Which.Ordem.Should().Be(2);
        lidas.Origem.PosicaoDeOrigem("documentosExigidos.regrasDeComprovacao", 0).Should().Be(1);
    }

    [Fact(DisplayName = "O grupo sem itens, sem forma ou com forma desconhecida é recusado sem exceção")]
    public void Grupo_Vazio_OuSemForma_NaoLanca()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Grupo(0, FormaDeComprovacao.TodosOsItens), Grupo(1, null, Folha(0, "identidade")), Folha(2, null));

        lidas.Exigencias.RegrasDeComprovacao.Should().BeEmpty();
        Causas(lidas).Should().NotBeEmpty();
        Causas(lidas).Select(static causa => causa.Ponteiro).Should().OnlyContain(ponteiro => ponteiro!.StartsWith(Raiz, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O grupo com item recusado fica sem composição conferida, e só a recusa do item é devolvida")]
    public void Grupo_ComItemRecusado_SoDevolveARecusaDoItem()
    {
        ExigenciasLidas lidas = Ler(DuasExigencias, Grupo(0, FormaDeComprovacao.TodosOsItens, Folha(0, "digitada")));

        Causas(lidas).Should().Equal(($"{Raiz}/0/itens/0/exigenciaChave", "RegraDeComprovacao.DocumentoObrigatorio"));
    }

    [Fact(DisplayName = "O grupo sem a escolha de prazo para reenvio é recusado no campo dela, e a regra de documento sem ela é aceita")]
    public void Grupo_SemEscolhaDeReenvio_Recusa()
    {
        RegraDeComprovacaoNoDocumento grupo = Grupo(0, FormaDeComprovacao.TodosOsItens, Folha(0, "identidade")) with { AbrePrazoParaReenvio = null };

        Causas(Ler(DuasExigencias, grupo)).Should().Equal(($"{Raiz}/0/abrePrazoParaReenvio", "uniplus.validacao.obrigatorio"));
        Causas(Ler(DuasExigencias, Folha(0, "identidade"))).Should().BeEmpty();
    }
}
