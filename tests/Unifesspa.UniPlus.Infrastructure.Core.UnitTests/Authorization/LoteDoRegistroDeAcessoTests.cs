namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Avro.IO;
using Avro.Specific;

using AwesomeAssertions;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A listagem grava um registro por inscrição exibida numa só mensagem do broker, que tem limite de tamanho: o lote da
/// maior página precisa ficar bem abaixo dele.
/// </summary>
public sealed class LoteDoRegistroDeAcessoTests
{
    private const int LimiteDaMensagemDoBrokerEmBytes = 1024 * 1024;

    [Fact(DisplayName = "O lote da maior página da listagem, com identificadores longos, fica abaixo da metade do limite da mensagem do broker")]
    public void LoteDaMaiorPagina_FicaBemAbaixoDoLimiteDoBroker()
    {
        CursorPaginationOptions paginacao = new();
        RegistroDecisaoAcesso origem = RegistroComIdentificadoresLongos();

        IReadOnlyList<RegistroDecisaoAcesso> lote = origem.ParaCadaResource(
            "inscricao", Enumerable.Range(0, paginacao.LimitMax).Select(static _ => Guid.CreateVersion7()), resourceEhInscricao: true);

        int bytes = TamanhoSerializado(MapeadorDoRegistroDeAcesso.ParaLote(lote));

        lote.Should().HaveCount(paginacao.LimitMax);
        bytes.Should().BeLessThan(LimiteDaMensagemDoBrokerEmBytes / 2);
    }

    private static RegistroDecisaoAcesso RegistroComIdentificadoresLongos()
    {
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From($"https://idp.exemplo/realms/unifesspa/{new string('r', 200)}", new string('s', 128)).Value!, "jti-teste").Value!;
        EffectiveGrant concessao = EffectiveGrant.DaConcessaoDePerfil("selecao:dados-pessoais:consultar", EscopoDaPermissaoEfetiva.Instituicao).Value!;

        return RegistroDecisaoAcesso.De(
            sujeito,
            PermissionRequirement.From("selecao:dados-pessoais:consultar", ClassificacaoDeProtecao.Pessoal, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!,
            ResourceContext.From("inscricao", ClassificacaoDeProtecao.Pessoal, Guid.CreateVersion7(), Guid.CreateVersion7()).Value!,
            AuthorizationRequestContext.From(new string('q', 256), DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!,
            AuthorizationDecision.Permitir(concessao));
    }

    private static int TamanhoSerializado(LoteDoRegistroDeAcesso lote)
    {
        using MemoryStream fluxo = new();
        new SpecificDatumWriter<LoteDoRegistroDeAcesso>(EsquemaDoRegistroDeAcesso.Lote).Write(lote, new BinaryEncoder(fluxo));

        return (int)fluxo.Length;
    }
}
