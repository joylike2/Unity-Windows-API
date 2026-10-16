using System.Runtime.CompilerServices;

// 작업표시줄 계산(DeskTaskbarMath) 같은 내부 순수 계산을 테스트 어셈블리에서 부를 수 있게 연다
[assembly: InternalsVisibleTo("LifeLogs.DeskWindows.Tests")]
